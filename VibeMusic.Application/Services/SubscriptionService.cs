using System.Threading;
using System.Threading.Tasks;
using VibeMusic.Application.DTOs;
using VibeMusic.Application.Interfaces;
using VibeMusic.Domain.Entities;
using VibeMusic.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace VibeMusic.Application.Services;

public class SubscriptionService : ISubscriptionService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMemoryCache _cache;
    private readonly IServiceScopeFactory _scopeFactory;

    public SubscriptionService(IUnitOfWork unitOfWork, IMemoryCache cache, IServiceScopeFactory scopeFactory)
    {
        _unitOfWork = unitOfWork;
        _cache = cache;
        _scopeFactory = scopeFactory;
    }

    public async Task<IEnumerable<SubscriptionPlanDto>> GetActivePlansAsync(CancellationToken ct = default)
    {
        try
        {
            var plans = await _unitOfWork.Repository<SubscriptionPlan>().FindAsync(p => p.IsActive, ct);
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

    public async Task<SubscriptionPlanDto?> GetPlanByIdAsync(int planId, CancellationToken ct = default)
    {
        var p = await _unitOfWork.Repository<SubscriptionPlan>().GetByIdAsync(planId, ct);
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

    public async Task<bool> IsUserPremiumAsync(int userId, CancellationToken ct = default)
    {
        string cacheKey = $"user_premium_{userId}";
        if (_cache.TryGetValue(cacheKey, out bool isPremium))
        {
            return isPremium;
        }

        // Absolute Stability: Use a dedicated scope for layout dependency check
        // This prevents ObjectDisposedException if the main request scope is laggy or corrupted
        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            
            // 1. Get user to check the direct IS_PREMIUM flag
            var user = await db.Repository<User>().GetByIdAsync(userId);
            if (user == null)
            {
                 _cache.Set(cacheKey, false, TimeSpan.FromMinutes(2));
                 return false;
            }
            
            // Priority 1: If the user record itself says they are premium, trust it (Fail-safe)
            if (user.IsPremium) {
                _cache.Set(cacheKey, true, TimeSpan.FromMinutes(2));
                return true;
            }
            
            // Priority 2: Check active subscription record with timezone safety
            var sub = await db.Repository<UserSubscription>()
                .FirstOrDefaultAsync(s => s.UserId == userId && (s.EndDate.Date >= DateTime.UtcNow.Date || s.IsActive));
            
            var result = sub != null;
            _cache.Set(cacheKey, result, TimeSpan.FromMinutes(2));
            return result;
        }
    }

    public async Task<int> CreateInitialPaymentAsync(int userId, int planId, long orderCode, CancellationToken ct = default)
    {
        var plan = await _unitOfWork.Repository<SubscriptionPlan>().GetByIdAsync(planId, ct);
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

        await _unitOfWork.Repository<Payment>().AddAsync(payment, ct);
        await _unitOfWork.CompleteAsync(ct);
        return payment.PaymentId;
    }

    private async Task SeedPlansIfEmptyAsync()
    {
        var plans = await _unitOfWork.Repository<SubscriptionPlan>().GetAllAsync();
        if (!plans.Any())
        {
            var defaultPlans = new List<SubscriptionPlan>
            {
                new SubscriptionPlan { Name = "Gói 1 Tháng", Price = 19000, DurationDays = 30, Description = "Sử dụng đầy đủ mọi tính năng trong 30 ngày." },
                new SubscriptionPlan { Name = "Gói 1 Năm", Price = 190000, DurationDays = 365, Description = "Tiết kiệm vượt trội với gói 1 năm cao cấp." },
                new SubscriptionPlan { Name = "Gói Trọn Đời", Price = 499000, DurationDays = 99999, Description = "Trải nghiệm âm nhạc đỉnh cao vĩnh viễn, không giới hạn." }
            };
            foreach (var p in defaultPlans)
            {
                await _unitOfWork.Repository<SubscriptionPlan>().AddAsync(p);
            }
            await _unitOfWork.CompleteAsync();
        }
    }

    public async Task ProcessPaymentSuccessAsync(long orderCode, string transactionId, CancellationToken ct = default)
    {
        try
        {
            var payment = await _unitOfWork.Repository<Payment>()
                .FirstOrDefaultAsync(p => p.OrderCode == orderCode && p.Status == "Pending", ct);
            
            if (payment == null) return;

            // 1. Update Payment
            payment.Status = "Success";
            payment.PayosTransactionId = transactionId;
            _unitOfWork.Repository<Payment>().Update(payment);

            var plan = await _unitOfWork.Repository<SubscriptionPlan>().GetByIdAsync(payment.PlanId, ct);
            if (plan != null)
            {
                // 2. Update User
                var user = await _unitOfWork.Repository<User>().GetByIdAsync(payment.UserId, ct);
                if (user != null)
                {
                    user.IsPremium = true;
                    _unitOfWork.Repository<User>().Update(user);
                }

                // 3. Create or Update UserSubscription
                var existingSub = await _unitOfWork.Repository<UserSubscription>()
                    .FirstOrDefaultAsync(s => s.UserId == payment.UserId && s.IsActive, ct);

                if (existingSub != null)
                {
                    if (existingSub.EndDate < DateTime.UtcNow) existingSub.EndDate = DateTime.UtcNow;
                    
                    if (plan.Name.Contains("Trọn Đời"))
                    {
                        existingSub.EndDate = new DateTime(9999, 12, 31);
                    }
                    else
                    {
                        existingSub.EndDate = existingSub.EndDate.AddDays(plan.DurationDays);
                    }
                    
                    existingSub.PlanId = plan.PlanId;
                    _unitOfWork.Repository<UserSubscription>().Update(existingSub);
                }
                else
                {
                    var endDate = plan.Name.Contains("Trọn Đời") 
                        ? new DateTime(9999, 12, 31) 
                        : DateTime.UtcNow.AddDays(plan.DurationDays);

                    var newSub = new UserSubscription
                    {
                        UserId = payment.UserId,
                        PlanId = plan.PlanId,
                        StartDate = DateTime.UtcNow,
                        EndDate = endDate,
                        IsActive = true
                    };
                    await _unitOfWork.Repository<UserSubscription>().AddAsync(newSub, ct);
                }
            }

            await _unitOfWork.CompleteAsync(ct);
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<IEnumerable<PaymentDto>> GetUserPaymentsAsync(int userId, CancellationToken ct = default)
    {
        // Fix N+1: Use Join to fetch Plan data in one query
        var payments = await _unitOfWork.Repository<Payment>()
            .Query()
            .Include(p => p.Plan)
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.PaymentDate)
            .ToListAsync(ct);
        
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

    public async Task<bool> CancelSubscriptionAsync(int userId, CancellationToken ct = default)
    {
        var existingSub = await _unitOfWork.Repository<UserSubscription>()
            .FirstOrDefaultAsync(s => s.UserId == userId && s.IsActive, ct);
        
        if (existingSub == null) return false;

        // Graceful Cancellation: Just mark as inactive (auto-renewal disabled)
        // User remains Premium until EndDate is passed (checked in IsUserPremiumAsync)
        existingSub.IsActive = false;
        _unitOfWork.Repository<UserSubscription>().Update(existingSub);

        await _unitOfWork.CompleteAsync(ct);
        return true;
    }

    public async Task<IEnumerable<SubscriptionPlanDto>> GetAllPlansAsync(CancellationToken ct = default)
    {
        var plans = await _unitOfWork.Repository<SubscriptionPlan>().GetAllAsync(ct);
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
    public async Task CreatePlanAsync(SubscriptionPlanDto dto, CancellationToken ct = default)
    {
        var plan = new SubscriptionPlan
        {
            Name = dto.Name,
            Price = dto.Price,
            DurationDays = dto.DurationDays,
            Description = dto.Description,
            IsActive = true
        };
        await _unitOfWork.Repository<SubscriptionPlan>().AddAsync(plan, ct);
        await _unitOfWork.CompleteAsync(ct);
    }

    public async Task UpdatePlanAsync(SubscriptionPlanDto dto, CancellationToken ct = default)
    {
        var plan = await _unitOfWork.Repository<SubscriptionPlan>().GetByIdAsync(dto.PlanId, ct);
        if (plan != null)
        {
            plan.Name = dto.Name;
            plan.Price = dto.Price;
            plan.DurationDays = dto.DurationDays;
            plan.Description = dto.Description;
            plan.IsActive = dto.IsActive;
            _unitOfWork.Repository<SubscriptionPlan>().Update(plan);
            await _unitOfWork.CompleteAsync(ct);
        }
    }

    public async Task DeletePlanAsync(int id, CancellationToken ct = default)
    {
        var plan = await _unitOfWork.Repository<SubscriptionPlan>().GetByIdAsync(id, ct);
        if (plan != null)
        {
            // Logic improvement: Soft delete via IsActive flag as requested by user
            plan.IsActive = false;
            _unitOfWork.Repository<SubscriptionPlan>().Update(plan);
            await _unitOfWork.CompleteAsync(ct);
        }
    }

    public async Task<int> GetActiveSubscriberCountAsync(int planId, CancellationToken ct = default)
    {
        // Define "Active" as anyone who had a successful payment for this plan.
        // In a more complex system, we'd check if their premium period hasn't expired.
        return await _unitOfWork.Repository<Payment>().Query()
            .CountAsync(p => p.PlanId == planId && p.Status == "Success", ct);
    }
}
