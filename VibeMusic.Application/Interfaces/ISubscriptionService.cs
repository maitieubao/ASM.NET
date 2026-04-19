using System.Threading;
using System.Threading.Tasks;
using YoutubeMusicPlayer.Application.DTOs;

namespace YoutubeMusicPlayer.Application.Interfaces;

public interface ISubscriptionService
{
    Task<IEnumerable<SubscriptionPlanDto>> GetActivePlansAsync(CancellationToken ct = default);
    Task<SubscriptionPlanDto?> GetPlanByIdAsync(int planId, CancellationToken ct = default);
    Task<bool> IsUserPremiumAsync(int userId, CancellationToken ct = default);
    Task<int> CreateInitialPaymentAsync(int userId, int planId, long orderCode, CancellationToken ct = default);
    Task ProcessPaymentSuccessAsync(long orderCode, string transactionId, CancellationToken ct = default);
    
    // New features for Use Case
    Task<IEnumerable<PaymentDto>> GetUserPaymentsAsync(int userId, CancellationToken ct = default);
    Task<bool> CancelSubscriptionAsync(int userId, CancellationToken ct = default);

    // Plan Management (Admin)
    Task<IEnumerable<SubscriptionPlanDto>> GetAllPlansAsync(CancellationToken ct = default);
    Task CreatePlanAsync(SubscriptionPlanDto dto, CancellationToken ct = default);
    Task UpdatePlanAsync(SubscriptionPlanDto dto, CancellationToken ct = default);
    Task DeletePlanAsync(int id, CancellationToken ct = default);
    Task<int> GetActiveSubscriberCountAsync(int planId, CancellationToken ct = default);
}
