using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PayOS;
using PayOS.Models.V2.PaymentRequests;
using PayOS.Models.Webhooks;
using YoutubeMusicPlayer.Application.Common;
using YoutubeMusicPlayer.Application.Interfaces;
using YoutubeMusicPlayer.Domain.Entities;
using YoutubeMusicPlayer.Domain.Interfaces;

namespace YoutubeMusicPlayer.Application.Services;

public class PayOSService : IPayOSService
{
    private readonly PayOSClient _payOS;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PayOSService> _logger;

    public PayOSService(IOptions<PayOSSettings> options, IUnitOfWork unitOfWork, ILogger<PayOSService> logger)
    {
        var settings = options.Value;
        
        // Debugging configuration load
        logger.LogInformation("[PayOS-DEBUG] ClientId Length: {CLen}, ApiKey Length: {ALen}, ChecksumKey Length: {SLen}", 
            settings.ClientId?.Length ?? 0, 
            settings.ApiKey?.Length ?? 0, 
            settings.ChecksumKey?.Length ?? 0);

        _payOS = new PayOSClient(settings.ClientId, settings.ApiKey, settings.ChecksumKey);
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<CreatePaymentLinkResponse> CreatePaymentLinkAsync(int userId, int planId, long orderCode, int amount, string description, string returnUrl, string cancelUrl)
    {
        try
        {
            // 1. Logic for Database tracking is moved entirely to SubscriptionService 
            // to avoid duplication errors (Unique Constraint on OrderCode).
            
            // 2. Optimized: Create request with correct expiration time (30 mins)
            var expiredAt = (int)DateTimeOffset.UtcNow.AddMinutes(30).ToUnixTimeSeconds();
            var item = new PaymentLinkItem 
            { 
                Name = description, 
                Quantity = 1, 
                Price = amount 
            };
            
            var request = new CreatePaymentLinkRequest
            {
                OrderCode = orderCode,
                Amount = amount,
                Description = description,
                Items = new List<PaymentLinkItem> { item },
                CancelUrl = cancelUrl,
                ReturnUrl = returnUrl,
                ExpiredAt = expiredAt
            };

            return await _payOS.PaymentRequests.CreateAsync(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PayOS] Failed to create payment link for UserID: {UserId}, OrderCode: {OrderCode}", userId, orderCode);
            throw;
        }
    }

    public async Task<PaymentLink> GetPaymentLinkInformationAsync(long orderCode)
    {
        try
        {
            return await _payOS.PaymentRequests.GetAsync(orderCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PayOS] Failed to retrieve payment info for OrderCode: {OrderCode}", orderCode);
            throw;
        }
    }

    public bool VerifyWebhookData(Webhook webhookData)
    {
        try
        {
            // Fallback to dynamic to bypass SDK model mismatch issues
            dynamic p = _payOS;
            return p.verifyPaymentData(webhookData) != null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PayOS] Webhook Signature Verification Failed.");
            return false;
        }
    }
}
