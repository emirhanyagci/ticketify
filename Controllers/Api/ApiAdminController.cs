using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ticketify.Models;
using Ticketify.Repositories;
using Ticketify.Services;

namespace Ticketify.Controllers.Api;

/// <summary>
/// Admin işlemleri — kullanıcı yönetimi ve ticket atama (sadece Admin rolü)
/// </summary>
[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
[Produces("application/json")]
public class ApiAdminController : ControllerBase
{
    private readonly ITicketRepository _ticketRepo;
    private readonly IUserRepository _userRepo;
    private readonly AuthService _authService;
    private readonly AvatarService _avatarService;

    public ApiAdminController(
        ITicketRepository ticketRepo,
        IUserRepository userRepo,
        AuthService authService,
        AvatarService avatarService)
    {
        _ticketRepo   = ticketRepo;
        _userRepo     = userRepo;
        _authService  = authService;
        _avatarService = avatarService;
    }

    private int CurrentAdminId()
        => int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;
    private string CurrentAdminEmail()
        => User.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty;
    private string CurrentAdminFullName()
        => User.FindFirst("FullName")?.Value ?? CurrentAdminEmail();

    // ================================================================ //
    //  KULLANICI YÖNETİMİ
    // ================================================================ //

    /// <summary>
    /// Tüm kullanıcıları listeler
    /// </summary>
    [HttpGet("users")]
    [ProducesResponseType(typeof(IEnumerable<UserDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllUsers()
    {
        var users = await _userRepo.GetAllAsync();
        return Ok(users.Select(UserDto.From));
    }

    /// <summary>
    /// Belirli bir kullanıcıyı getirir
    /// </summary>
    [HttpGet("users/{id:int}")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUser(int id)
    {
        var user = await _userRepo.GetByIdAsync(id);
        if (user == null)
            return NotFound(new ErrorResponse { Message = $"#{id} numaralı kullanıcı bulunamadı." });

        return Ok(UserDto.From(user));
    }

    /// <summary>
    /// Yeni kullanıcı oluşturur (Admin tarafından — Employee veya User rolü atanabilir)
    /// </summary>
    [HttpPost("users")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateUser([FromBody] AdminCreateUserRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var existing = await _userRepo.GetByEmailAsync(request.Email);
        if (existing != null)
            return Conflict(new ErrorResponse { Message = "Bu e-posta adresi zaten kayıtlı." });

        var validRoles = new[] { "User", "Employee", "Admin" };
        if (!validRoles.Contains(request.Role))
            return BadRequest(new ErrorResponse { Message = "Geçersiz rol. User | Employee | Admin" });

        var fullName = string.IsNullOrWhiteSpace(request.FullName)
            ? request.Email.Split('@')[0]
            : request.FullName.Trim();

        var user = new User
        {
            FullName     = fullName,
            Email        = request.Email,
            PasswordHash = _authService.HashPassword(request.Password),
            Role         = request.Role,
            Department   = request.Department,
            AvatarUrl    = _avatarService.GenerateAvatarUrl(request.Email)
        };

        await _userRepo.CreateAsync(user);
        return CreatedAtAction(nameof(GetUser), new { id = user.Id }, UserDto.From(user));
    }

    /// <summary>
    /// Kullanıcı rolünü ve departmanını günceller
    /// </summary>
    [HttpPatch("users/{id:int}")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserRequest request)
    {
        var user = await _userRepo.GetByIdAsync(id);
        if (user == null)
            return NotFound(new ErrorResponse { Message = $"#{id} numaralı kullanıcı bulunamadı." });

        if (!string.IsNullOrWhiteSpace(request.Role))
            user.Role = request.Role;
        if (request.Department != null)
            user.Department = request.Department;
        if (!string.IsNullOrWhiteSpace(request.FullName))
            user.FullName = request.FullName;

        await _userRepo.UpdateAsync(user);
        return Ok(UserDto.From(user));
    }

    /// <summary>
    /// Kullanıcıyı siler
    /// </summary>
    [HttpDelete("users/{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteUser(int id)
    {
        var user = await _userRepo.GetByIdAsync(id);
        if (user == null)
            return NotFound(new ErrorResponse { Message = $"#{id} numaralı kullanıcı bulunamadı." });

        await _userRepo.DeleteAsync(id);
        return Ok(new { message = $"#{id} numaralı kullanıcı silindi." });
    }

    // ================================================================ //
    //  TİCKET YÖNETİMİ (Admin)
    // ================================================================ //

    /// <summary>
    /// Ticket'ı bir çalışana atar
    /// </summary>
    [HttpPost("tickets/{ticketId:int}/assign")]
    [ProducesResponseType(typeof(TicketSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignTicket(int ticketId, [FromBody] AssignTicketRequest request)
    {
        var ticket = await _ticketRepo.GetByIdAsync(ticketId);
        if (ticket == null)
            return NotFound(new ErrorResponse { Message = $"#{ticketId} numaralı ticket bulunamadı." });

        var employee = await _userRepo.GetByIdAsync(request.EmployeeId);
        if (employee == null || employee.Role != "Employee")
            return BadRequest(new ErrorResponse { Message = "Geçersiz çalışan ID'si." });

        var now = DateTime.UtcNow;
        ticket.AssigneeId          = employee.Id;
        ticket.AssigneeEmail       = employee.Email;
        ticket.AssigneeFullName    = employee.FullName;
        ticket.AssigneeAvatarUrl   = employee.AvatarUrl;
        ticket.AssignedById        = CurrentAdminId();
        ticket.AssignedByEmail     = CurrentAdminEmail();
        ticket.AssignedByFullName  = CurrentAdminFullName();
        ticket.AssignedAt          = now;
        ticket.UpdatedAt           = now;

        await _ticketRepo.UpdateAsync(ticket);
        return Ok(TicketSummaryDto.From(ticket));
    }

    /// <summary>
    /// Ticket atamasını kaldırır
    /// </summary>
    [HttpDelete("tickets/{ticketId:int}/assign")]
    [ProducesResponseType(typeof(TicketSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UnassignTicket(int ticketId)
    {
        var ticket = await _ticketRepo.GetByIdAsync(ticketId);
        if (ticket == null)
            return NotFound(new ErrorResponse { Message = $"#{ticketId} numaralı ticket bulunamadı." });

        ticket.AssigneeId         = null;
        ticket.AssigneeEmail      = null;
        ticket.AssigneeFullName   = null;
        ticket.AssigneeAvatarUrl  = null;
        ticket.AssignedAt         = null;
        ticket.UpdatedAt          = DateTime.UtcNow;

        await _ticketRepo.UpdateAsync(ticket);
        return Ok(TicketSummaryDto.From(ticket));
    }

    /// <summary>
    /// Ticket'ı siler (sadece Admin)
    /// </summary>
    [HttpDelete("tickets/{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTicket(int id)
    {
        var deleted = await _ticketRepo.DeleteAsync(id);
        if (!deleted)
            return NotFound(new ErrorResponse { Message = $"#{id} numaralı ticket bulunamadı." });

        return Ok(new { message = $"#{id} numaralı ticket silindi." });
    }
}

// ---- DTO'lar ----

/// <summary>Kullanıcı bilgileri</summary>
public record UserDto
{
    public int Id { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public string? Department { get; init; }
    public string? AvatarUrl { get; init; }
    public DateTime CreatedAt { get; init; }

    public static UserDto From(User u) => new()
    {
        Id         = u.Id,
        FullName   = u.FullName,
        Email      = u.Email,
        Role       = u.Role,
        Department = u.Department,
        AvatarUrl  = u.AvatarUrl,
        CreatedAt  = u.CreatedAt
    };
}

/// <summary>Admin tarafından kullanıcı oluşturma isteği</summary>
public record AdminCreateUserRequest(
    /// <example>employee@ticketify.com</example>
    string Email,
    /// <example>Mehmet Demir</example>
    string? FullName,
    /// <example>Güvenli123!</example>
    string Password,
    /// <example>Employee</example>
    string Role,
    /// <example>Software</example>
    string? Department
);

/// <summary>Kullanıcı güncelleme isteği</summary>
public record UpdateUserRequest(
    /// <example>Employee</example>
    string? Role,
    /// <example>Network</example>
    string? Department,
    /// <example>Ali Veli</example>
    string? FullName
);

/// <summary>Ticket atama isteği</summary>
public record AssignTicketRequest(
    /// <example>2</example>
    int EmployeeId
);
