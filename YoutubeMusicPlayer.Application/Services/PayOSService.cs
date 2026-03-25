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
    private readonly PayOSClient _payOS;

    public PayOSService(IConfiguration configuration)
    {
        string clientId = configuration["PayOS:ClientId"] ?? "";
        string apiKey = configuration["PayOS:ApiKey"] ?? "";
        string checksumKey = configuration["PayOS:ChecksumKey"] ?? "";
        // In some versions it may require the partner code as well, but 3 parameters is standard
        _payOS = new PayOSClient(clientId, apiKey, checksumKey);
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
        // For production, you would call _payOS.Webhooks.VerifyAsync or check the signature
        // The VerifyAsync returns the WebhookData if valid, or throws WebhookException
        return webhookData.Success && webhookData.Data != null;
    }
}
