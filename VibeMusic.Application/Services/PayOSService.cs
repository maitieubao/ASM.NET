using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PayOS;
using PayOS.Models.V2.PaymentRequests;
using PayOS.Models.Webhooks;
using VibeMusic.Application.Common;
using VibeMusic.Application.Interfaces;
using VibeMusic.Domain.Entities;
using VibeMusic.Domain.Interfaces;

    /// <summary>
    /// Handles integration with the PayOS payment gateway (Vietnam).
    /// Manages secure payment link generation and webhook signature verification.
    /// </summary>
    public class PayOSService : IPayOSService
    {
        private readonly PayOSClient _payOS;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<PayOSService> _logger;

        public PayOSService(IOptions<PayOSSettings> options, IUnitOfWork unitOfWork, ILogger<PayOSService> logger)
        {
            var settings = options.Value;
            
            // SECURITY: Ensure all credentials are loaded from secure appsettings/secrets.
            _payOS = new PayOSClient(
                settings.ClientId ?? string.Empty, 
                settings.ApiKey ?? string.Empty, 
                settings.ChecksumKey ?? string.Empty);
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<CreatePaymentLinkResponse> CreatePaymentLinkAsync(int userId, int planId, long orderCode, int amount, string description, string returnUrl, string cancelUrl)
        {
            try
            {
                // BUSINESS RULE: Payment links expire in 30 minutes to prevent stale order codes.
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

        /// <summary>
        /// Verifies the authenticity of the webhook data received from PayOS.
        /// </summary>
        /// <param name="webhookData">The payload received from PayOS.</param>
        /// <returns>True if the signature is valid.</returns>
        public bool VerifyWebhookData(Webhook webhookData)
        {
            try
            {
                /* 
                 * WORKAROUND/HACK: 
                 * The PayOS SDK 1.0.x has strict model validation that sometimes fails 
                 * during signature verification if the input model isn't exactly the SDK's internal version.
                 * We use 'dynamic' to bypass compile-time checks and call the raw verification method.
                 */
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
