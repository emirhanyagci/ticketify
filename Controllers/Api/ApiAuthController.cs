using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Ticketify.Repositories;
using Ticketify.Services;

namespace Ticketify.Controllers.Api;

/// <summary>
/// Kimlik doğrulama işlemleri (kayıt / giriş / çıkış)
/// </summary>
[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class ApiAuthController : ControllerBase
{
    private readonly IUserRepository _userRepo;
    private readonly AuthService _authService;
    private readonly AvatarService _avatarService;

    public ApiAuthController(IUserRepository userRepo, AuthService authService, AvatarService avatarService)
    {
        _userRepo = userRepo;
        _authService = authService;
        _avatarService = avatarService;
    }

    /// <summary>
    /// Yeni kullanıcı kaydı oluşturur (varsayılan rol: User)
    /// </summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new ErrorResponse { Message = "Geçersiz istek formatı." });

        var existing = await _userRepo.GetByEmailAsync(request.Email);
        if (existing != null)
            return Conflict(new ErrorResponse { Message = "Bu e-posta adresi zaten kayıtlı." });

        var fullName = string.IsNullOrWhiteSpace(request.FullName)
            ? request.Email.Split('@')[0]
            : request.FullName.Trim();

        var user = new Ticketify.Models.User
        {
            FullName     = fullName,
            Email        = request.Email,
            PasswordHash = _authService.HashPassword(request.Password),
            Role         = "User",
            AvatarUrl    = _avatarService.GenerateAvatarUrl(request.Email)
        };

        await _userRepo.CreateAsync(user);

        return CreatedAtAction(nameof(Register), new LoginResponse
        {
            Id        = user.Id,
            FullName  = user.FullName,
            Email     = user.Email,
            Role      = user.Role,
            AvatarUrl = user.AvatarUrl,
            Message   = "Hesabınız oluşturuldu."
        });
    }

    /// <summary>
    /// Sisteme giriş yapar ve cookie oturumu başlatır
    /// </summary>
    /// <param name="request">Email ve şifre bilgileri</param>
    /// <returns>Kullanıcı bilgileri veya hata mesajı</returns>
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new ErrorResponse { Message = "Geçersiz istek formatı." });

        var user = await _userRepo.GetByEmailAsync(request.Email);
        if (user == null || !_authService.VerifyPassword(user, request.Password))
            return Unauthorized(new ErrorResponse { Message = "Email veya şifre hatalı." });

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

        return Ok(new LoginResponse
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            Role = user.Role,
            AvatarUrl = user.AvatarUrl,
            Message = "Giriş başarılı."
        });
    }

    /// <summary>
    /// Oturumu sonlandırır
    /// </summary>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync("CookieAuth");
        return Ok(new { message = "Çıkış yapıldı." });
    }
}

// ---- DTO'lar ----

/// <summary>Kayıt isteği</summary>
public record RegisterRequest(
    /// <example>user@ticketify.com</example>
    string Email,
    /// <example>Güvenli123!</example>
    string Password,
    /// <example>Ahmet Yılmaz</example>
    string? FullName
);

/// <summary>Login isteği</summary>
public record LoginRequest(
    /// <example>admin@ticketify.com</example>
    string Email,
    /// <example>Admin123!</example>
    string Password
);

/// <summary>Başarılı auth yanıtı</summary>
public record LoginResponse
{
    public int Id { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public string? AvatarUrl { get; init; }
    public string Message { get; init; } = string.Empty;
}

/// <summary>Hata yanıtı</summary>
public record ErrorResponse
{
    public string Message { get; init; } = string.Empty;
}
