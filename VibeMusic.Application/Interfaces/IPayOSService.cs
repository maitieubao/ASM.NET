using PayOS.Models.V2.PaymentRequests;
using PayOS.Models.Webhooks;

namespace YoutubeMusicPlayer.Application.Interfaces;

public interface IPayOSService
{
    Task<CreatePaymentLinkResponse> CreatePaymentLinkAsync(int userId, int planId, long orderCode, int amount, string description, string returnUrl, string cancelUrl);
    Task<PaymentLink> GetPaymentLinkInformationAsync(long orderCode);
    bool VerifyWebhookData(Webhook webhookData);
}
