using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using YoutubeMusicPlayer.Application.DTOs;
using YoutubeMusicPlayer.Application.Interfaces;
using YoutubeMusicPlayer.Domain.Entities;
using YoutubeMusicPlayer.Domain.Interfaces;

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
            await SeedPlansIfEmptyAsync();
            var plans = await _unitOfWork.Repository<SubscriptionPlan>().GetAllAsync();
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
            // Log the error for diagnosing. Returns empty if DB table is missing but caught by controller.
            // If the table doesn't exist yet, SeedPlansIfEmptyAsync will throw.
            throw new Exception("Lỗi khi tải gói hội viên. Đảm bảo bạn đã chạy Migration hoặc File SQL trên Supabase. Chi tiết: " + ex.Message);
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
        
        // If flag is true, also check if subscription is still valid
        if (user.IsPremium)
        {
            var activeSub = await _unitOfWork.Repository<UserSubscription>()
                .FirstOrDefaultAsync(s => s.UserId == userId && s.IsActive && s.EndDate > DateTime.UtcNow);
            
            if (activeSub == null)
            {
                // Expired
                user.IsPremium = false;
                _unitOfWork.Repository<User>().Update(user);
                await _unitOfWork.CompleteAsync();
                return false;
            }
            return true;
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
        var payment = await _unitOfWork.Repository<Payment>()
            .FirstOrDefaultAsync(p => p.OrderCode == orderCode && p.Status == "Pending");
        
        if (payment == null) return;

        payment.Status = "Success";
        payment.PayosTransactionId = transactionId;
        _unitOfWork.Repository<Payment>().Update(payment);

        // Activate Subscription
        var plan = await _unitOfWork.Repository<SubscriptionPlan>().GetByIdAsync(payment.PlanId);
        if (plan != null)
        {
            // Update User
            var user = await _unitOfWork.Repository<User>().GetByIdAsync(payment.UserId);
            if (user != null)
            {
                user.IsPremium = true;
                _unitOfWork.Repository<User>().Update(user);
            }

            // Create or Update UserSubscription
            var existingSub = await _unitOfWork.Repository<UserSubscription>()
                .FirstOrDefaultAsync(s => s.UserId == payment.UserId && s.IsActive);

            if (existingSub != null)
            {
                // EXTEND existing
                if (existingSub.EndDate < DateTime.UtcNow) existingSub.EndDate = DateTime.UtcNow;
                existingSub.EndDate = existingSub.EndDate.AddDays(plan.DurationDays);
                existingSub.PlanId = plan.PlanId;
                _unitOfWork.Repository<UserSubscription>().Update(existingSub);
            }
            else
            {
                // NEW
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
    }
}
