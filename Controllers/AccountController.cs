using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Ticketify.Models;
using Ticketify.Repositories;
using Ticketify.Services;
using Ticketify.ViewModels;

namespace Ticketify.Controllers;

public class AccountController : Controller
{
    private readonly IUserRepository _userRepo;
    private readonly AuthService _authService;
    private readonly AvatarService _avatarService;

    public AccountController(IUserRepository userRepo, AuthService authService, AvatarService avatarService)
    {
        _userRepo = userRepo;
        _authService = authService;
        _avatarService = avatarService;
    }

    // GET: /Account/Login
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectBasedOnRole();

        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    // POST: /Account/Login
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = await _userRepo.GetByEmailAsync(model.Email);
        if (user == null || !_authService.VerifyPassword(user, model.Password))
        {
            ModelState.AddModelError(string.Empty, "E-posta adresi veya şifre hatalı.");
            return View(model);
        }

        // Avatar yoksa oluştur (mevcut kullanıcılar için migration)
        if (string.IsNullOrEmpty(user.AvatarUrl))
        {
            user.AvatarUrl = _avatarService.GenerateAvatarUrl(user.Email);
            await _userRepo.UpdateAsync(user);
        }
        if (string.IsNullOrEmpty(user.FullName))
        {
            user.FullName = user.Email.Split('@')[0];
            await _userRepo.UpdateAsync(user);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.Email),
            new(ClaimTypes.Role, user.Role),
            new("FullName", user.FullName),
            new("AvatarUrl", user.AvatarUrl ?? "")
        };

        var identity = new ClaimsIdentity(claims, "CookieAuth");
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync("CookieAuth", principal);

        TempData["Success"] = $"Hoş geldiniz, {user.FullName}!";

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectBasedOnRole(user.Role);
    }

    // GET: /Account/Register
    [HttpGet]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectBasedOnRole();

        return View();
    }

    // POST: /Account/Register
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        // Duplicate email kontrolü
        var existing = await _userRepo.GetByEmailAsync(model.Email);
        if (existing != null)
        {
            ModelState.AddModelError("Email", "Bu e-posta adresi zaten kayıtlı.");
            return View(model);
        }

        var fullName = model.Email.Split('@')[0]; // E-postadan varsayılan ad

        var user = new User
        {
            FullName = fullName,
            Email = model.Email,
            PasswordHash = _authService.HashPassword(model.Password),
            Role = "User",
            AvatarUrl = _avatarService.GenerateAvatarUrl(model.Email)
        };

        await _userRepo.CreateAsync(user);

        TempData["Success"] = "Hesabınız oluşturuldu. Giriş yapabilirsiniz.";
        return RedirectToAction("Login");
    }

    // POST: /Account/Logout
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync("CookieAuth");
        TempData["Success"] = "Başarıyla çıkış yaptınız.";
        return RedirectToAction("Login");
    }

    // GET: /Account/AccessDenied
    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }

    // Yardımcı: Role göre yönlendir
    private IActionResult RedirectBasedOnRole(string? role = null)
    {
        role ??= User.FindFirst(ClaimTypes.Role)?.Value ?? "User";
        return role switch
        {
            "Admin" => RedirectToAction("Dashboard", "Admin"),
            "Employee" => RedirectToAction("Index", "Ticket"),
            _ => RedirectToAction("Index", "Ticket")
        };
    }
}
