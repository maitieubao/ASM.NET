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
}
