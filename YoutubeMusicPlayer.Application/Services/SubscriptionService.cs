using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using YoutubeMusicPlayer.Application.DTOs;
using YoutubeMusicPlayer.Application.Interfaces;
using YoutubeMusicPlayer.Domain.Entities;
using YoutubeMusicPlayer.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace YoutubeMusicPlayer.Application.Services;

public class SubscriptionService : ISubscriptionService
{
    private readonly IUnitOfWork _unitOfWork;

    public SubscriptionService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<SubscriptionPlanDto>> GetActivePlansAsync()
    {
        try
        {
            var plans = await _unitOfWork.Repository<SubscriptionPlan>().FindAsync(p => p.IsActive);
            return plans.Select(p => new SubscriptionPlanDto
            {
                PlanId = p.PlanId,
                Name = p.Name,
                Price = p.Price,
                DurationDays = p.DurationDays,
                Description = p.Description
            });
        }
        catch (Exception ex)
        {
            throw new Exception("Lỗi khi tải gói hội viên. Chi tiết: " + ex.Message);
        }
    }

    public async Task<SubscriptionPlanDto?> GetPlanByIdAsync(int planId)
    {
        var p = await _unitOfWork.Repository<SubscriptionPlan>().GetByIdAsync(planId);
        if (p == null) return null;
        return new SubscriptionPlanDto
        {
            PlanId = p.PlanId,
            Name = p.Name,
            Price = p.Price,
            DurationDays = p.DurationDays,
            Description = p.Description
        };
    }

    public async Task<bool> IsUserPremiumAsync(int userId)
    {
        var user = await _unitOfWork.Repository<User>().GetByIdAsync(userId);
        if (user == null) return false;
        
        if (user.IsPremium)
        {
            // Just check validity, don't update DB here (Side effect removal)
            var activeSub = await _unitOfWork.Repository<UserSubscription>()
                .FirstOrDefaultAsync(s => s.UserId == userId && s.IsActive && s.EndDate > DateTime.UtcNow);
            
            return activeSub != null;
        }
        return false;
    }

    public async Task<int> CreateInitialPaymentAsync(int userId, int planId, long orderCode)
    {
        var plan = await _unitOfWork.Repository<SubscriptionPlan>().GetByIdAsync(planId);
        if (plan == null) return 0;

        var payment = new Payment
        {
            UserId = userId,
            PlanId = planId,
            Amount = plan.Price,
            OrderCode = orderCode,
            Status = "Pending",
            PaymentDate = DateTime.UtcNow
        };

        await _unitOfWork.Repository<Payment>().AddAsync(payment);
        await _unitOfWork.CompleteAsync();
        return payment.PaymentId;
    }

    private async Task SeedPlansIfEmptyAsync()
    {
        var plans = await _unitOfWork.Repository<SubscriptionPlan>().GetAllAsync();
        if (!plans.Any())
        {
            var defaultPlans = new List<SubscriptionPlan>
            {
                new SubscriptionPlan { Name = "Gói 1 Tháng", Price = 59000, DurationDays = 30, Description = "Sử dụng đầy đủ mọi tính năng trong 30 ngày." },
                new SubscriptionPlan { Name = "Gói 3 Tháng", Price = 159000, DurationDays = 90, Description = "Tiết kiệm hơn với gói 3 tháng cao cấp." },
                new SubscriptionPlan { Name = "Gói 1 Năm", Price = 499000, DurationDays = 365, Description = "Trải nghiệm âm nhạc đỉnh cao cả năm." }
            };
            foreach (var p in defaultPlans)
            {
                await _unitOfWork.Repository<SubscriptionPlan>().AddAsync(p);
            }
            await _unitOfWork.CompleteAsync();
        }
    }

    public async Task ProcessPaymentSuccessAsync(long orderCode, string transactionId)
    {
        using var transaction = await _unitOfWork.BeginTransactionAsync();
        try
        {
            var payment = await _unitOfWork.Repository<Payment>()
                .FirstOrDefaultAsync(p => p.OrderCode == orderCode && p.Status == "Pending");
            
            if (payment == null) return;

            // 1. Update Payment
            payment.Status = "Success";
            payment.PayosTransactionId = transactionId;
            _unitOfWork.Repository<Payment>().Update(payment);

            var plan = await _unitOfWork.Repository<SubscriptionPlan>().GetByIdAsync(payment.PlanId);
            if (plan != null)
            {
                // 2. Update User
                var user = await _unitOfWork.Repository<User>().GetByIdAsync(payment.UserId);
                if (user != null)
                {
                    user.IsPremium = true;
                    _unitOfWork.Repository<User>().Update(user);
                }

                // 3. Create or Update UserSubscription
                var existingSub = await _unitOfWork.Repository<UserSubscription>()
                    .FirstOrDefaultAsync(s => s.UserId == payment.UserId && s.IsActive);

                if (existingSub != null)
                {
                    if (existingSub.EndDate < DateTime.UtcNow) existingSub.EndDate = DateTime.UtcNow;
                    existingSub.EndDate = existingSub.EndDate.AddDays(plan.DurationDays);
                    existingSub.PlanId = plan.PlanId;
                    _unitOfWork.Repository<UserSubscription>().Update(existingSub);
                }
                else
                {
                    var newSub = new UserSubscription
                    {
                        UserId = payment.UserId,
                        PlanId = plan.PlanId,
                        StartDate = DateTime.UtcNow,
                        EndDate = DateTime.UtcNow.AddDays(plan.DurationDays),
                        IsActive = true
                    };
                    await _unitOfWork.Repository<UserSubscription>().AddAsync(newSub);
                }
            }

            await _unitOfWork.CompleteAsync();
            await transaction.CommitAsync();
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<IEnumerable<PaymentDto>> GetUserPaymentsAsync(int userId)
    {
        // Fix N+1: Use Join to fetch Plan data in one query
        var payments = await _unitOfWork.Repository<Payment>()
            .Query()
            .Include(p => p.Plan)
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.PaymentDate)
            .ToListAsync();
        
        return payments.Select(p => new PaymentDto
        {
            PaymentId = p.PaymentId,
            UserId = p.UserId,
            PlanId = p.PlanId,
            PlanName = p.Plan?.Name ?? "Gói đã xóa",
            Amount = p.Amount,
            Status = p.Status,
            PaymentDate = p.PaymentDate,
            OrderCode = p.OrderCode
        });
    }

    public async Task<bool> CancelSubscriptionAsync(int userId)
    {
        var existingSub = await _unitOfWork.Repository<UserSubscription>()
            .FirstOrDefaultAsync(s => s.UserId == userId && s.IsActive);
        
        if (existingSub == null) return false;

        // Graceful Cancellation: Just mark as inactive (auto-renewal disabled)
        // User remains Premium until EndDate is passed (checked in IsUserPremiumAsync)
        existingSub.IsActive = false;
        _unitOfWork.Repository<UserSubscription>().Update(existingSub);

        await _unitOfWork.CompleteAsync();
        return true;
    }

    public async Task<IEnumerable<SubscriptionPlanDto>> GetAllPlansAsync()
    {
        var plans = await _unitOfWork.Repository<SubscriptionPlan>().GetAllAsync();
        return plans.Select(p => new SubscriptionPlanDto
        {
            PlanId = p.PlanId,
            Name = p.Name,
            Price = p.Price,
            DurationDays = p.DurationDays,
            Description = p.Description,
            IsActive = p.IsActive
        });
    }

    public async Task CreatePlanAsync(SubscriptionPlanDto dto)
    {
        var plan = new SubscriptionPlan
        {
            Name = dto.Name,
            Price = dto.Price,
            DurationDays = dto.DurationDays,
            Description = dto.Description,
            IsActive = true
        };
        await _unitOfWork.Repository<SubscriptionPlan>().AddAsync(plan);
        await _unitOfWork.CompleteAsync();
    }

    public async Task UpdatePlanAsync(SubscriptionPlanDto dto)
    {
        var plan = await _unitOfWork.Repository<SubscriptionPlan>().GetByIdAsync(dto.PlanId);
        if (plan != null)
        {
            plan.Name = dto.Name;
            plan.Price = dto.Price;
            plan.DurationDays = dto.DurationDays;
            plan.Description = dto.Description;
            plan.IsActive = dto.IsActive;
            _unitOfWork.Repository<SubscriptionPlan>().Update(plan);
            await _unitOfWork.CompleteAsync();
        }
    }

    public async Task DeletePlanAsync(int id)
    {
        var plan = await _unitOfWork.Repository<SubscriptionPlan>().GetByIdAsync(id);
        if (plan != null)
        {
            plan.IsActive = false; // Soft delete usually better for billing
            _unitOfWork.Repository<SubscriptionPlan>().Update(plan);
            await _unitOfWork.CompleteAsync();
        }
    }
}
