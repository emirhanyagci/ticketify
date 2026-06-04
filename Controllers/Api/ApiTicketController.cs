using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ticketify.Models;
using Ticketify.Repositories;
using Ticketify.Services;

namespace Ticketify.Controllers.Api;

/// <summary>
/// Destek talebi (ticket) yönetimi
/// </summary>
[ApiController]
[Route("api/tickets")]
[Authorize]
[Produces("application/json")]
public class ApiTicketController : ControllerBase
{
    private readonly ITicketRepository _ticketRepo;
    private readonly IUserRepository _userRepo;
    private readonly ICommentRepository _commentRepo;
    private readonly AvatarService _avatarService;

    public ApiTicketController(
        ITicketRepository ticketRepo,
        IUserRepository userRepo,
        ICommentRepository commentRepo,
        AvatarService avatarService)
    {
        _ticketRepo = ticketRepo;
        _userRepo = userRepo;
        _commentRepo = commentRepo;
        _avatarService = avatarService;
    }

    private int CurrentUserId()
        => int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;
    private string CurrentUserEmail()
        => User.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty;
    private string CurrentUserFullName()
        => User.FindFirst("FullName")?.Value ?? CurrentUserEmail();
    private string CurrentUserRole()
        => User.FindFirst(ClaimTypes.Role)?.Value ?? "User";

    // ------------------------------------------------------------------ //
    //  GET /api/tickets
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Kullanıcıya ait ticket listesini getirir.
    /// Admin tüm ticket'ları görür; Employee sadece atanmış olanları; User sadece kendi açtıklarını.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<TicketSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var userId = CurrentUserId();
        var role = CurrentUserRole();

        IEnumerable<Ticket> tickets = role switch
        {
            "Admin"    => await _ticketRepo.GetAllAsync(),
            "Employee" => (await _ticketRepo.GetAllAsync()).Where(t => t.AssigneeId == userId),
            _          => await _ticketRepo.GetByUserIdAsync(userId)
        };

        var result = tickets.Select(TicketSummaryDto.From);
        return Ok(result);
    }

    // ------------------------------------------------------------------ //
    //  GET /api/tickets/{id}
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Belirli bir ticket'ın detaylarını getirir
    /// </summary>
    /// <param name="id">Ticket ID</param>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(TicketDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var ticket = await _ticketRepo.GetByIdAsync(id);
        if (ticket == null)
            return NotFound(new ErrorResponse { Message = $"#{id} numaralı ticket bulunamadı." });

        var userId = CurrentUserId();
        var role = CurrentUserRole();

        if (role == "User" && ticket.UserId != userId)
            return Forbid();
        if (role == "Employee" && ticket.AssigneeId != userId)
            return Forbid();

        var comments = (await _commentRepo.GetByTicketIdAsync(id)).ToList();

        return Ok(new TicketDetailDto
        {
            Ticket = TicketSummaryDto.From(ticket),
            Comments = comments.Select(CommentDto.From).ToList()
        });
    }

    // ------------------------------------------------------------------ //
    //  POST /api/tickets
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Yeni destek talebi oluşturur (User ve Admin rolleri)
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "User,Admin")]
    [ProducesResponseType(typeof(TicketSummaryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateTicketRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userId = CurrentUserId();
        var user = await _userRepo.GetByIdAsync(userId);

        var ticket = new Ticket
        {
            Title       = request.Title,
            Description = request.Description,
            Priority    = request.Priority,
            Department  = request.Department,
            Status      = "Open",
            UserId      = userId,
            UserEmail   = CurrentUserEmail(),
            UserFullName   = user?.FullName ?? CurrentUserEmail(),
            UserAvatarUrl  = user?.AvatarUrl ?? _avatarService.GenerateAvatarUrl(CurrentUserEmail())
        };

        await _ticketRepo.CreateAsync(ticket);

        var dto = TicketSummaryDto.From(ticket);
        return CreatedAtAction(nameof(GetById), new { id = ticket.Id }, dto);
    }

    // ------------------------------------------------------------------ //
    //  PATCH /api/tickets/{id}/status
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Ticket durumunu günceller (Employee ve Admin rolleri)
    /// </summary>
    /// <param name="id">Ticket ID</param>
    /// <param name="request">Yeni durum: Open | InProgress | Resolved</param>
    [HttpPatch("{id:int}/status")]
    [Authorize(Roles = "Employee,Admin")]
    [ProducesResponseType(typeof(TicketSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateStatusRequest request)
    {
        var valid = new[] { "Open", "InProgress", "Resolved" };
        if (!valid.Contains(request.Status))
            return BadRequest(new ErrorResponse { Message = "Geçersiz durum. Open | InProgress | Resolved" });

        var ticket = await _ticketRepo.GetByIdAsync(id);
        if (ticket == null)
            return NotFound(new ErrorResponse { Message = $"#{id} numaralı ticket bulunamadı." });

        var userId = CurrentUserId();
        var role   = CurrentUserRole();

        if (role == "Employee" && ticket.AssigneeId != userId)
            return Forbid();

        var now = DateTime.UtcNow;
        ticket.Status    = request.Status;
        ticket.UpdatedAt = now;

        if (request.Status == "InProgress" && ticket.InProgressAt == null) ticket.InProgressAt = now;
        else if (request.Status == "Resolved" && ticket.ResolvedAt == null) ticket.ResolvedAt = now;
        else if (request.Status == "Open") { ticket.InProgressAt = null; ticket.ResolvedAt = null; }

        await _ticketRepo.UpdateAsync(ticket);
        return Ok(TicketSummaryDto.From(ticket));
    }

    // ------------------------------------------------------------------ //
    //  POST /api/tickets/{id}/comments
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Ticket'a yorum ekler (Employee ve Admin rolleri)
    /// </summary>
    [HttpPost("{id:int}/comments")]
    [Authorize(Roles = "Employee,Admin")]
    [ProducesResponseType(typeof(CommentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddComment(int id, [FromBody] AddCommentRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var ticket = await _ticketRepo.GetByIdAsync(id);
        if (ticket == null)
            return NotFound(new ErrorResponse { Message = $"#{id} numaralı ticket bulunamadı." });

        var userId = CurrentUserId();
        var user   = await _userRepo.GetByIdAsync(userId);

        var comment = new Comment
        {
            TicketId       = id,
            AuthorId       = userId,
            AuthorEmail    = CurrentUserEmail(),
            AuthorFullName = user?.FullName ?? CurrentUserEmail(),
            AuthorAvatarUrl = user?.AvatarUrl ?? _avatarService.GenerateAvatarUrl(CurrentUserEmail()),
            AuthorRole     = CurrentUserRole(),
            Content        = request.Content.Trim()
        };

        await _commentRepo.CreateAsync(comment);
        return CreatedAtAction(nameof(GetById), new { id }, CommentDto.From(comment));
    }
}

// ------------------------------------------------------------------ //
//  DTO'lar
// ------------------------------------------------------------------ //

/// <summary>Ticket özet bilgileri</summary>
public record TicketSummaryDto
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Priority { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Department { get; init; } = string.Empty;
    public string UserFullName { get; init; } = string.Empty;
    public string UserEmail { get; init; } = string.Empty;
    public string? AssigneeFullName { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }

    public static TicketSummaryDto From(Ticket t) => new()
    {
        Id = t.Id, Title = t.Title, Description = t.Description,
        Priority = t.Priority, Status = t.Status, Department = t.Department,
        UserFullName = t.UserFullName, UserEmail = t.UserEmail,
        AssigneeFullName = t.AssigneeFullName,
        CreatedAt = t.CreatedAt, UpdatedAt = t.UpdatedAt
    };
}

/// <summary>Ticket detay (ticket + yorumlar)</summary>
public record TicketDetailDto
{
    public TicketSummaryDto Ticket { get; init; } = null!;
    public List<CommentDto> Comments { get; init; } = new();
}

/// <summary>Yorum bilgileri</summary>
public record CommentDto
{
    public int Id { get; init; }
    public string AuthorFullName { get; init; } = string.Empty;
    public string AuthorRole { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }

    public static CommentDto From(Comment c) => new()
    {
        Id = c.Id, AuthorFullName = c.AuthorFullName, AuthorRole = c.AuthorRole,
        Content = c.Content, CreatedAt = c.CreatedAt
    };
}

/// <summary>Yeni ticket oluşturma isteği</summary>
public record CreateTicketRequest(
    /// <example>VPN bağlantısı kopuyor</example>
    string Title,
    /// <example>Uzak masaüstü bağlantısı kurulamıyor.</example>
    string Description,
    /// <example>High</example>
    string Priority,
    /// <example>Network</example>
    string Department
);

/// <summary>Durum güncelleme isteği</summary>
public record UpdateStatusRequest(
    /// <example>InProgress</example>
    string Status
);

/// <summary>Yorum ekleme isteği</summary>
public record AddCommentRequest(
    /// <example>Sorunu inceledik, çözüm üzerinde çalışıyoruz.</example>
    string Content
);
