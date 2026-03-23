using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading.Tasks;
using YoutubeMusicPlayer.Application.DTOs;
using YoutubeMusicPlayer.Application.Interfaces;
using System.Collections.Generic;

namespace YoutubeMusicPlayer.Controllers;

public class AuthController : Controller
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login()
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
    public async Task<IActionResult> Login(LoginDto model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await _authService.AuthenticateAsync(model.Email, model.Password);
        if (user == null)
        {
            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            return View(model);
        }

        await SignInUser(user, model.RememberMe);
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
    public IActionResult LoginWithGoogle()
    {
        var properties = new AuthenticationProperties { RedirectUri = Url.Action("GoogleResponse") };
        return Challenge(properties, GoogleDefaults.AuthenticationScheme);
    }

    [AllowAnonymous]
    public async Task<IActionResult> GoogleResponse()
    {
        var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        
        // ASP.NET Core usually puts external login claims into Cookie directly if we didn't specify an External scheme, 
        // but typically one should use a separate external scheme. For simplicity we check if we got claims.
        // Wait, since we are doing direct cookie login with Google, result is from Cookie scheme.
        // BUT wait, Google will redirect to signin-google and use the default sign-in scheme (Cookie).
        // Let's get info.
        
        var claims = result.Principal?.Identities.FirstOrDefault()?.Claims;
        if (claims == null) return RedirectToAction("Login");

        var email = claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
        var name = claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;
        var nameIdentifier = claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
        
        // Google picture is usually under this claim
        var picture = claims.FirstOrDefault(c => c.Type == "picture")?.Value 
                   ?? claims.FirstOrDefault(c => c.Type == "image")?.Value;

        if (email == null || nameIdentifier == null)
        {
            return RedirectToAction("Login");
        }

        try
        {
            var user = await _authService.AuthenticateGoogleUserAsync(email, name ?? "Unknown", nameIdentifier, picture);
            
            // Re-issue cookie with our app's claims
            await SignInUser(user, true);

            return RedirectToAction("Index", "Home");
        }
        catch (System.Exception ex)
        {
            // Trả về lỗi rõ ràng thay vì trang trắng chết chóc
            ModelState.AddModelError(string.Empty, "Lỗi kết nối Cơ sở dữ liệu Supabase: " + ex.Message);
            return View("Login"); // Trả về trang đăng nhập kèm lỗi
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }

    private async Task SignInUser(UserDto user, bool isPersistent)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role),
        };

        if (!string.IsNullOrEmpty(user.AvatarUrl))
        {
            // Adding a custom claim for Avatar
            claims.Add(new Claim("AvatarUrl", user.AvatarUrl));
        }

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        
        var authProperties = new AuthenticationProperties
        {
            IsPersistent = isPersistent
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme, 
            new ClaimsPrincipal(claimsIdentity), 
            authProperties);
    }
}
