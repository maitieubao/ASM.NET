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
        _payOS = new PayOSClient(settings.ClientId, settings.ApiKey, settings.ChecksumKey);
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<CreatePaymentLinkResponse> CreatePaymentLinkAsync(int userId, int planId, long orderCode, int amount, string description, string returnUrl, string cancelUrl)
    {
        try
        {
            // 1. Optimized: Persistent Tracking - Save record BEFORE calling external API
            var payment = new Payment
            {
                UserId = userId,
                PlanId = planId,
                OrderCode = orderCode,
                Amount = amount,
                Status = PaymentStatus.Pending,
                PaymentDate = DateTime.UtcNow
            };

            await _unitOfWork.Repository<Payment>().AddAsync(payment);
            await _unitOfWork.CompleteAsync();

            // 2. Optimized: Correct nested SDK call
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
                ReturnUrl = returnUrl
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
            // Note: Using dynamic as a fallback if the SDK method is not publicly typed in this version, 
            // but we wrap it in a safe try-catch with logging.
            dynamic p = _payOS;
            var verifiedData = p.verifyPaymentData(webhookData);
            return verifiedData != null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PayOS] Webhook Signature Verification Failed.");
            return false;
        }
    }
}
