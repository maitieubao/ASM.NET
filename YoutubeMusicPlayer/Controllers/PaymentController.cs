using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PayOS.Models.Webhooks;
using PayOS.Models.V2.PaymentRequests;
using YoutubeMusicPlayer.Application.Interfaces;

namespace YoutubeMusicPlayer.Controllers;

// Gỡ bỏ Authorize ở cấp toàn bộ Controller để tránh lỗi 401 cứng của trình duyệt
public class PaymentController : Controller
{
    private readonly IPayOSService _payOSService;
    private readonly ISubscriptionService _subscriptionService;
    private readonly IAuthService _authService;

    public PaymentController(IPayOSService payOSService, ISubscriptionService subscriptionService, IAuthService authService)
    {
        _payOSService = payOSService;
        _subscriptionService = subscriptionService;
        _authService = authService;
    }

    private async Task<int> GetCurrentUserIdAsync()
    {
        // 1. Thử lấy ID nội bộ (số nguyên)
        var internalIdClaim = User.FindFirst("InternalUserId");
        if (internalIdClaim != null && int.TryParse(internalIdClaim.Value, out int internalId))
        {
            return internalId;
        }

        // 2. Nếu không có hoặc là Google ID (số quá lớn), thử tìm trong DB qua Email
        var emailClaim = User.FindFirst(System.Security.Claims.ClaimTypes.Email);
        if (emailClaim != null)
        {
            var user = await _authService.AuthenticateGoogleUserAsync(emailClaim.Value, User.Identity?.Name ?? "", "", "");
            Console.WriteLine($"DEBUG: Resolved Large ID to Internal ID: {user.UserId}");
            return user.UserId;
        }

        return 0;
    }

    [HttpGet]
    public async Task<IActionResult> CreatePayment(int planId = 0)
    {
        Console.WriteLine($"DEBUG: GET CreatePayment - planId: {planId}");
        
        if (planId == 0) return RedirectToAction("Index", "Subscription");

        var plan = await _subscriptionService.GetPlanByIdAsync(planId);
        if (plan == null) {
            Console.WriteLine("DEBUG: Plan not found.");
            return NotFound();
        }

        return View(plan);
    }

    [HttpPost]
    [ActionName("CreatePayment")]
    public async Task<IActionResult> CreatePaymentPost(int planId)
    {
        Console.WriteLine($"DEBUG: POST CreatePayment - planId: {planId}");
        var userId = await GetCurrentUserIdAsync();
        
        if (userId == 0) return RedirectToAction("Login", "Auth");

        var plan = await _subscriptionService.GetPlanByIdAsync(planId);
        if (plan == null) return NotFound("Plan not found");

        long orderCode = long.Parse(DateTimeOffset.Now.ToString("ddHHmmss")); 
        string description = $"Thanh toan {plan.Name}";
        string returnUrl = Url.Action("Success", "Payment", new { orderCode = orderCode }, Request.Scheme) ?? "";
        string cancelUrl = Url.Action("Cancel", "Payment", null, Request.Scheme) ?? "";

        try
        {
            await _subscriptionService.CreateInitialPaymentAsync(userId, planId, orderCode);
            var result = await _payOSService.CreatePaymentLinkAsync(userId, planId, orderCode, (int)plan.Price, description, returnUrl, cancelUrl);
            
            Console.WriteLine($"DEBUG: Redirecting to PayOS Checkout: {result.CheckoutUrl}");
            return Redirect(result.CheckoutUrl);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DEBUG ERROR: {ex.Message}");
            TempData["Error"] = "Lỗi khởi tạo thanh toán: " + ex.Message;
            return RedirectToAction("CreatePayment", new { planId = planId });
        }
    }

    [HttpGet]
    public async Task<IActionResult> Success(long orderCode, string id, string status)
    {
        // "id" here is the checkout ID from PayOS
        if (status == "PAID" || status == "COMPLETED")
        {
            await _subscriptionService.ProcessPaymentSuccessAsync(orderCode, id);
            return View();
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
        // PayOS will call this when payment is confirmed
        if (webhookData.Data != null && _payOSService.VerifyWebhookData(webhookData))
        {
             await _subscriptionService.ProcessPaymentSuccessAsync(webhookData.Data.OrderCode, webhookData.Data.PaymentLinkId);
             return Ok();
        }
        return BadRequest();
    }
}
