using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using YoutubeMusicPlayer.Application.DTOs;

namespace YoutubeMusicPlayer.Application.Interfaces;

public interface ISubscriptionService
{
    Task<IEnumerable<SubscriptionPlanDto>> GetActivePlansAsync();
    Task<SubscriptionPlanDto?> GetPlanByIdAsync(int planId);
    Task<bool> IsUserPremiumAsync(int userId);
    Task<int> CreateInitialPaymentAsync(int userId, int planId, long orderCode);
    Task ProcessPaymentSuccessAsync(long orderCode, string transactionId);
    
    // New features for Use Case
    Task<IEnumerable<PaymentDto>> GetUserPaymentsAsync(int userId);
    Task<bool> CancelSubscriptionAsync(int userId);

    // Plan Management (Admin)
    Task<IEnumerable<SubscriptionPlanDto>> GetAllPlansAsync();
    Task CreatePlanAsync(SubscriptionPlanDto dto);
    Task UpdatePlanAsync(SubscriptionPlanDto dto);
    Task DeletePlanAsync(int id);
}
