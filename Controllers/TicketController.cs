using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ticketify.Models;
using Ticketify.Repositories;
using Ticketify.Services;
using Ticketify.ViewModels;

namespace Ticketify.Controllers;

[Authorize]
public class TicketController : Controller
{
    private readonly ITicketRepository _ticketRepo;
    private readonly IUserRepository _userRepo;
    private readonly ICommentRepository _commentRepo;
    private readonly AvatarService _avatarService;

    public TicketController(
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

    private int GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(claim, out var id) ? id : 0;
    }

    private string GetCurrentUserEmail()
        => User.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty;

    private string GetCurrentUserFullName()
        => User.FindFirst("FullName")?.Value ?? GetCurrentUserEmail();

    private string GetCurrentUserRole()
        => User.FindFirst(ClaimTypes.Role)?.Value ?? "User";

    private string? GetCurrentUserAvatarUrl()
        => User.FindFirst("AvatarUrl")?.Value;

    // GET: /Ticket/Index
    public async Task<IActionResult> Index()
    {
        var userId = GetCurrentUserId();
        var role = GetCurrentUserRole();

        IEnumerable<Ticket> tickets;

        // Employee sadece kendisine atanan ticket'ları görür
        if (role == "Employee")
        {
            var allTickets = await _ticketRepo.GetAllAsync();
            tickets = allTickets.Where(t => t.AssigneeId == userId);
        }
        else
        {
            tickets = await _ticketRepo.GetByUserIdAsync(userId);
        }

        var vm = new TicketListViewModel
        {
            Tickets = tickets,
            UserEmail = GetCurrentUserEmail(),
            UserFullName = GetCurrentUserFullName(),
            UserRole = role
        };

        return View(vm);
    }

    // GET: /Ticket/Create
    [Authorize(Roles = "User,Admin")]
    public IActionResult Create()
    {
        return View(new TicketCreateViewModel());
    }

    // POST: /Ticket/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "User,Admin")]
    public async Task<IActionResult> Create(TicketCreateViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var userId = GetCurrentUserId();
        var user = await _userRepo.GetByIdAsync(userId);

        var ticket = new Ticket
        {
            Title = model.Title,
            Description = model.Description,
            Priority = model.Priority,
            Department = model.Department,
            Status = "Open",
            UserId = userId,
            UserEmail = GetCurrentUserEmail(),
            UserFullName = user?.FullName ?? GetCurrentUserEmail(),
            UserAvatarUrl = user?.AvatarUrl ?? _avatarService.GenerateAvatarUrl(GetCurrentUserEmail())
        };

        await _ticketRepo.CreateAsync(ticket);

        TempData["Success"] = "Destek talebiniz başarıyla oluşturuldu!";
        return RedirectToAction("Index");
    }

    // GET: /Ticket/Details/{id}
    public async Task<IActionResult> Details(int id)
    {
        var ticket = await _ticketRepo.GetByIdAsync(id);
        if (ticket == null)
        {
            TempData["Error"] = "Ticket bulunamadı.";
            return RedirectToAction("Index");
        }

        var userId = GetCurrentUserId();
        var role = GetCurrentUserRole();

        // Erişim kontrolü: User sadece kendi ticket'larını görebilir
        if (role == "User" && ticket.UserId != userId)
            return Forbid();

        // Employee sadece atandığı ticket'ları görebilir
        if (role == "Employee" && ticket.AssigneeId != userId)
            return Forbid();

        var comments = (await _commentRepo.GetByTicketIdAsync(id)).ToList();
        var employees = (await _userRepo.GetByRoleAsync("Employee")).ToList();

        // Timeline oluştur
        var timeline = new List<TimelineEvent>
        {
            new()
            {
                Icon = "bi-plus-circle-fill",
                IconColor = "#6366f1",
                Label = "Ticket Oluşturuldu",
                Timestamp = ticket.CreatedAt,
                Detail = $"{ticket.UserFullName} tarafından açıldı"
            }
        };

        if (ticket.AssignedAt.HasValue)
        {
            timeline.Add(new()
            {
                Icon = "bi-person-check-fill",
                IconColor = "#8b5cf6",
                Label = "Çalışana Atandı",
                Timestamp = ticket.AssignedAt.Value,
                Detail = $"{ticket.AssigneeFullName ?? "Bilinmiyor"} — {(ticket.AssignedByFullName ?? "Admin")} tarafından atandı"
            });
        }

        if (ticket.InProgressAt.HasValue)
        {
            timeline.Add(new()
            {
                Icon = "bi-arrow-repeat",
                IconColor = "#f59e0b",
                Label = "İşleme Alındı",
                Timestamp = ticket.InProgressAt.Value,
                Detail = "Durum 'İşlemde' olarak güncellendi"
            });
        }

        if (ticket.ResolvedAt.HasValue)
        {
            timeline.Add(new()
            {
                Icon = "bi-check-circle-fill",
                IconColor = "#10b981",
                Label = "Çözüldü",
                Timestamp = ticket.ResolvedAt.Value,
                Detail = "Ticket başarıyla çözümlendi"
            });
        }

        // Yorum olaylarını timeline'a ekle
        foreach (var comment in comments)
        {
            timeline.Add(new()
            {
                Icon = "bi-chat-fill",
                IconColor = "#64748b",
                Label = "Yorum Eklendi",
                Timestamp = comment.CreatedAt,
                Detail = $"{comment.AuthorFullName}: {(comment.Content.Length > 50 ? comment.Content[..50] + "…" : comment.Content)}"
            });
        }

        // Timeline'ı kronolojik sıraya koy
        timeline = timeline.OrderBy(e => e.Timestamp).ToList();

        var vm = new TicketDetailViewModel
        {
            Ticket = ticket,
            Comments = comments,
            AvailableEmployees = employees,
            Timeline = timeline,
            CanComment = role is "Employee" or "Admin",
            CanAssign = role == "Admin",
            // Admin her zaman, Employee sadece kendisine atanan ticket'ta güncelleyebilir
            CanUpdateStatus = role == "Admin" || (role == "Employee" && ticket.AssigneeId == userId)
        };

        return View(vm);
    }

    // POST: /Ticket/AddComment
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Employee,Admin")]
    public async Task<IActionResult> AddComment(int ticketId, string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            TempData["Error"] = "Yorum içeriği boş olamaz.";
            return RedirectToAction("Details", new { id = ticketId });
        }

        if (content.Length > 2000)
        {
            TempData["Error"] = "Yorum en fazla 2000 karakter olabilir.";
            return RedirectToAction("Details", new { id = ticketId });
        }

        var ticket = await _ticketRepo.GetByIdAsync(ticketId);
        if (ticket == null)
        {
            TempData["Error"] = "Ticket bulunamadı.";
            return RedirectToAction("Index");
        }

        var userId = GetCurrentUserId();
        var user = await _userRepo.GetByIdAsync(userId);

        var comment = new Comment
        {
            TicketId = ticketId,
            AuthorId = userId,
            AuthorEmail = GetCurrentUserEmail(),
            AuthorFullName = user?.FullName ?? GetCurrentUserEmail(),
            AuthorAvatarUrl = user?.AvatarUrl ?? _avatarService.GenerateAvatarUrl(GetCurrentUserEmail()),
            AuthorRole = GetCurrentUserRole(),
            Content = content.Trim()
        };

        await _commentRepo.CreateAsync(comment);

        TempData["Success"] = "Yorumunuz eklendi.";
        return RedirectToAction("Details", new { id = ticketId });
    }

    // POST: /Ticket/UpdateStatus — Admin veya atanan Employee kullanabilir
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Employee,Admin")]
    public async Task<IActionResult> UpdateStatus(int id, string status)
    {
        var validStatuses = new[] { "Open", "InProgress", "Resolved" };
        if (!validStatuses.Contains(status))
        {
            TempData["Error"] = "Geçersiz durum değeri.";
            return RedirectToAction("Details", new { id });
        }

        var ticket = await _ticketRepo.GetByIdAsync(id);
        if (ticket == null)
        {
            TempData["Error"] = "Ticket bulunamadı.";
            return RedirectToAction("Index");
        }

        var userId = GetCurrentUserId();
        var role = GetCurrentUserRole();

        // Yetki kontrolü: Employee sadece kendi üzerindeki ticket'ı güncelleyebilir
        if (role == "Employee" && ticket.AssigneeId != userId)
            return Forbid();

        var now = DateTime.UtcNow;
        ticket.Status = status;
        ticket.UpdatedAt = now;

        if (status == "InProgress" && ticket.InProgressAt == null)
            ticket.InProgressAt = now;
        else if (status == "Resolved" && ticket.ResolvedAt == null)
            ticket.ResolvedAt = now;
        else if (status == "Open")
        {
            ticket.InProgressAt = null;
            ticket.ResolvedAt = null;
        }

        await _ticketRepo.UpdateAsync(ticket);

        var statusLabel = status switch
        {
            "Open"       => "Açık",
            "InProgress" => "İşlemde",
            "Resolved"   => "Çözüldü",
            _            => status
        };
        TempData["Success"] = $"#{id} numaralı ticket durumu güncellendi: {statusLabel}";
        return RedirectToAction("Details", new { id });
    }

    // POST: /Ticket/DeleteComment
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteComment(int commentId, int ticketId)
    {
        await _commentRepo.DeleteAsync(commentId);
        TempData["Success"] = "Yorum silindi.";
        return RedirectToAction("Details", new { id = ticketId });
    }
}
