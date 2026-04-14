using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PayOS.Models.Webhooks;
using PayOS.Models.V2.PaymentRequests;
using YoutubeMusicPlayer.Application.Interfaces;
using YoutubeMusicPlayer.Application.DTOs;

namespace YoutubeMusicPlayer.Controllers;

public class PaymentController : BaseController
{
    private readonly IPayOSService _payOSService;
    private readonly ISubscriptionService _subscriptionService;
    private readonly ILogger<PaymentController> _logger;

    public PaymentController(IPayOSService payOSService, 
                             ISubscriptionService subscriptionService,
                             ILogger<PaymentController> logger)
    {
        _payOSService = payOSService;
        _subscriptionService = subscriptionService;
        _logger = logger;
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> CreatePayment(int planId = 0)
    {
        _logger.LogInformation("GET CreatePayment initialized for planId: {PlanId}", planId);
        
        if (planId == 0) return RedirectToAction("Index", "Subscription");

        var plan = await _subscriptionService.GetPlanByIdAsync(planId);
        if (plan == null) {
            _logger.LogWarning("Subscription plan {PlanId} not found.", planId);
            return NotFound();
        }

        return View(plan);
    }

    [HttpPost]
    [Authorize]
    [ActionName("CreatePayment")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreatePaymentPost(int planId)
    {
        if (CurrentUserId == null) return RedirectToAction("Login", "Auth");

        var plan = await _subscriptionService.GetPlanByIdAsync(planId);
        if (plan == null) return NotFound("Plan not found");

        // 1. Đảm bảo OrderCode duy nhất và an toàn (12-13 chữ số)
        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        int randomPart = new Random().Next(100, 999);
        long orderCode = long.Parse($"{timestamp % 1000000000}{randomPart}"); // 9 digits timestamp + 3 digits random

        // 2. Bỏ dấu hoàn toàn cho mô tả (PayOS cực kỳ khắt khe phần này)
        string cleanName = RemoveDiacritics(plan.Name);
        string description = $"Thanh toan {cleanName}";
        if (description.Length > 25) description = description.Substring(0, 25);
        string returnUrl = Url.Action("Success", "Payment", new { orderCode = orderCode }, Request.Scheme) ?? "";
        string cancelUrl = Url.Action("Cancel", "Payment", null, Request.Scheme) ?? "";

        try
        {
            _logger.LogInformation("Creating payment record in DB for User {UserId}, OrderCode {OrderCode}", CurrentUserId, orderCode);
            await _subscriptionService.CreateInitialPaymentAsync(CurrentUserId.Value, planId, orderCode);
            
            var result = await _payOSService.CreatePaymentLinkAsync(CurrentUserId.Value, planId, orderCode, (int)plan.Price, description, returnUrl, cancelUrl);
            
            _logger.LogInformation("Redirecting User {UserId} to PayOS Checkout: {CheckoutUrl}", CurrentUserId, result.CheckoutUrl);
            return Redirect(result.CheckoutUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize payment for User {UserId} and OrderCode {OrderCode}", CurrentUserId, orderCode);
            TempData["Error"] = "Lỗi khi khởi tạo thanh toán. Vui lòng thử lại sau.";
            return RedirectToAction("CreatePayment", new { planId = planId });
        }
    }

    [HttpGet]
    public async Task<IActionResult> Success(long orderCode)
    {
        try
        {
            var paymentInfo = await _payOSService.GetPaymentLinkInformationAsync(orderCode);
            
            if (paymentInfo.Status.ToString() == "PAID" || paymentInfo.Status.ToString() == "COMPLETED")
            {
                _logger.LogInformation("Verified PAID status for OrderCode {OrderCode} via PayOS API.", orderCode);
                // PayOS uses id and status in some versions, but if it is mapped to PascalCase, use Status and Id
                await _subscriptionService.ProcessPaymentSuccessAsync(orderCode, paymentInfo.Id);
                return View();
            }

            _logger.LogWarning("Verification failed for OrderCode {OrderCode}. Status: {Status}", orderCode, paymentInfo.Status);
            TempData["Message"] = "Giao dịch hiện đang chờ xử lý hoặc chưa hoàn thành.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying payment for OrderCode {OrderCode}", orderCode);
        }
        
        return RedirectToAction("Index", "Subscription");
    }

    [HttpGet]
    public IActionResult Cancel()
    {
        TempData["Message"] = "Bạn đã hủy quy trình thanh toán. Vui lòng chọn lại gói mong muốn.";
        return RedirectToAction("Index", "Subscription");
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> Webhook([FromBody] Webhook webhookData)
    {
        try 
        {
            if (webhookData.Data != null && _payOSService.VerifyWebhookData(webhookData))
            {
                _logger.LogInformation("Webhook received and verified for OrderCode {OrderCode}", webhookData.Data.OrderCode);
                await _subscriptionService.ProcessPaymentSuccessAsync(webhookData.Data.OrderCode, webhookData.Data.PaymentLinkId);
                return Ok();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical error processing Webhook for OrderCode {OrderCode}", webhookData?.Data?.OrderCode);
        }
        
        return BadRequest();
    }

    private string RemoveDiacritics(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;
        
        string[] arr1 = new string[] { "á", "à", "ả", "ã", "ạ", "â", "ấ", "ầ", "ẩ", "ẫ", "ậ", "ă", "ắ", "ằ", "ẳ", "ẵ", "ặ",
            "đ",
            "é","è","ẻ","ẽ","ẹ","ê","ế","ề","ể","ễ","ệ",
            "í","ì","ỉ","ĩ","ị",
            "ó","ò","ỏ","õ","ọ","ô","ố","ồ","ổ","ỗ","ộ","ơ","ớ","ờ","ở","ỡ","ợ",
            "ú","ù","ủ","ũ","ụ","ư","ứ","ừ","ử","ữ","ự",
            "ý","ỳ","ỷ","ỹ","ỵ",};
        string[] arr2 = new string[] { "a", "a", "a", "a", "a", "a", "a", "a", "a", "a", "a", "a", "a", "a", "a", "a", "a",
            "d",
            "e","e","e","e","e","e","e","e","e","e","e",
            "i","i","i","i","i",
            "o","o","o","o","o","o","o","o","o","o","o","o","o","o","o","o","o",
            "u","u","u","u","u","u","u","u","u","u","u",
            "y","y","y","y","y",};
        for (int i = 0; i < arr1.Length; i++)
        {
            text = text.Replace(arr1[i], arr2[i]);
            text = text.Replace(arr1[i].ToUpper(), arr2[i].ToUpper());
        }
        return text;
    }
}
