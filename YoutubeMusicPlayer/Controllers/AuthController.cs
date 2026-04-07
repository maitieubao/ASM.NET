using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Collections.Generic;
using YoutubeMusicPlayer.Application.DTOs;
using YoutubeMusicPlayer.Application.Interfaces;
using YoutubeMusicPlayer.Application.Common;

namespace YoutubeMusicPlayer.Controllers;

public class AuthController : Controller
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return string.IsNullOrEmpty(returnUrl) ? 
                RedirectToAction("Index", "Home") : Redirect(returnUrl);
        }
        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginDto model, string? returnUrl = null)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await _authService.AuthenticateAsync(model.Email, model.Password);
        if (user == null)
        {
            ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không chính xác.");
            return View(model);
        }

        await SignInUser(user, model.RememberMe);
        
        // Security: Open Redirect protection
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }
        return View();
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterDto model)
    {
        if (!ModelState.IsValid) return View(model);

        try
        {
            var user = await _authService.RegisterAsync(model);
            await SignInUser(user, false);
            return RedirectToAction("Index", "Home");
        }
        catch (System.Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    [AllowAnonymous]
    public IActionResult LoginWithGoogle(string? returnUrl = null)
    {
        var properties = new AuthenticationProperties { RedirectUri = Url.Action("GoogleResponse", new { returnUrl }) };
        return Challenge(properties, GoogleDefaults.AuthenticationScheme);
    }

    [AllowAnonymous]
    public async Task<IActionResult> GoogleResponse(string? returnUrl = null)
    {
        var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        
        var claims = result.Principal?.Identities.FirstOrDefault()?.Claims;
        if (claims == null) return RedirectToAction("Login", new { returnUrl });

        var email = claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
        var name = claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;
        var nameIdentifier = claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
        
        var picture = claims.FirstOrDefault(c => c.Type == "picture")?.Value 
                   ?? claims.FirstOrDefault(c => c.Type == "image")?.Value;

        if (email == null || nameIdentifier == null)
        {
            return RedirectToAction("Login", new { returnUrl });
        }

        try
        {
            var user = await _authService.AuthenticateGoogleUserAsync(email, name ?? "Unknown", nameIdentifier, picture);
            
            await SignInUser(user, true);

            // Security: Open Redirect protection
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction("Index", "Home");
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "Lỗi đăng nhập Google cho email {Email}", email);
            ModelState.AddModelError(string.Empty, "Không thể hoàn tất đăng nhập bằng Google lúc này. Vui lòng thử lại sau.");
            return View("Login"); 
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }

    // Forgot Password Flow
    [HttpGet]
    [AllowAnonymous]
    public IActionResult ForgotPassword() => View();

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(string email)
    {
        var token = await _authService.ForgotPasswordAsync(email);
        
        // Security: Prevent Email Enumeration by returning a neutral message
        TempData["Message"] = "Nếu email này tồn tại trong hệ thống, chúng tôi đã gửi mã xác nhận.";
        
        if (token != null)
        {
            // Debug purpose only - remove in pure production env
            TempData["DebugToken"] = token;
        }

        return RedirectToAction(nameof(ResetPassword), new { email });
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult ResetPassword(string email) => View(new { email });

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(string email, string token, string newPassword)
    {
        var success = await _authService.ResetPasswordAsync(email, token, newPassword);
        if (success)
        {
            TempData["Message"] = "Password reset successful. Please login.";
            return RedirectToAction(nameof(Login));
        }
        ModelState.AddModelError("", "Invalid or expired token.");
        return View();
    }

    private async Task SignInUser(UserDto user, bool isPersistent)
    {
        var claims = new List<Claim>
        {
            new Claim(AuthConstants.InternalUserIdClaim, user.UserId.ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("IsPremium", user.IsPremium.ToString()),
        };

        if (!string.IsNullOrEmpty(user.AvatarUrl))
        {
            claims.Add(new Claim("AvatarUrl", user.AvatarUrl));
        }

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        
        var authProperties = new AuthenticationProperties
        {
            IsPersistent = isPersistent,
            IssuedUtc = DateTimeOffset.UtcNow,
            ExpiresUtc = isPersistent ? DateTimeOffset.UtcNow.AddDays(7) : DateTimeOffset.UtcNow.AddHours(2),
            AllowRefresh = true
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme, 
            new ClaimsPrincipal(claimsIdentity), 
            authProperties);
    }
}
