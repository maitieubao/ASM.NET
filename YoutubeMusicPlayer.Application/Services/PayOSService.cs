using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using PayOS;
using PayOS.Models.V2.PaymentRequests;
using PayOS.Models.Webhooks;
using YoutubeMusicPlayer.Application.Interfaces;

namespace YoutubeMusicPlayer.Application.Services;

public class PayOSService : IPayOSService
{
    private readonly PayOS.PayOSClient _payOS;

    public PayOSService(IConfiguration configuration)
    {
        string clientId = configuration["PayOS:ClientId"] ?? "";
        string apiKey = configuration["PayOS:ApiKey"] ?? "";
        string checksumKey = configuration["PayOS:ChecksumKey"] ?? "";
        _payOS = new PayOS.PayOSClient(clientId, apiKey, checksumKey);
    }

    public async Task<CreatePaymentLinkResponse> CreatePaymentLinkAsync(int userId, int planId, long orderCode, int amount, string description, string returnUrl, string cancelUrl)
    {
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

    public async Task<PaymentLink> GetPaymentLinkInformationAsync(long orderCode)
    {
        return await _payOS.PaymentRequests.GetAsync(orderCode);
    }

    public bool VerifyWebhookData(Webhook webhookData)
    {
        try
        {
            // The PayOS .NET SDK handles signature verification via the client
            dynamic p = _payOS;
            var verifiedData = p.verifyPaymentData(webhookData);
            return verifiedData != null && webhookData.Success;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PayOS] Webhook Verification Failed: {ex.Message}");
            return false;
        }
    }
}
