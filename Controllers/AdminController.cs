using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ticketify.Models;
using Ticketify.Repositories;
using Ticketify.ViewModels;

namespace Ticketify.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly ITicketRepository _ticketRepo;
    private readonly IUserRepository _userRepo;

    public AdminController(ITicketRepository ticketRepo, IUserRepository userRepo)
    {
        _ticketRepo = ticketRepo;
        _userRepo = userRepo;
    }

    private int GetCurrentUserId()
        => int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;

    private string GetCurrentUserEmail()
        => User.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty;

    private string GetCurrentUserFullName()
        => User.FindFirst("FullName")?.Value ?? GetCurrentUserEmail();

    // GET: /Admin/Dashboard
    public async Task<IActionResult> Dashboard(
        string? status = null,
        string? department = null,
        string? search = null,
        string? priority = null,
        string? assignee = null,
        DateTime? dateFrom = null,
        DateTime? dateTo = null)
    {
        var allTickets = (await _ticketRepo.GetAllAsync()).ToList();
        var employees = (await _userRepo.GetByRoleAsync("Employee")).ToList();
        var activeTicketCounts = GetActiveTicketCounts(allTickets, employees);
        var suggestedAssignees = allTickets
            .Where(t => t.AssigneeId == null)
            .Select(t => new { TicketId = t.Id, Employee = FindBestAssignee(t, employees, activeTicketCounts) })
            .Where(x => x.Employee != null)
            .ToDictionary(x => x.TicketId, x => x.Employee!);

        var filtered = allTickets.AsEnumerable();
        if (!string.IsNullOrEmpty(status))
            filtered = filtered.Where(t => t.Status == status);
        if (!string.IsNullOrEmpty(department))
            filtered = filtered.Where(t => t.Department == department);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            filtered = filtered.Where(t =>
                ContainsIgnoreCase(t.Title, term) ||
                ContainsIgnoreCase(t.Description, term) ||
                ContainsIgnoreCase(t.UserFullName, term) ||
                ContainsIgnoreCase(t.UserEmail, term) ||
                ContainsIgnoreCase(t.AssigneeFullName, term) ||
                ContainsIgnoreCase(t.AssigneeEmail, term));
        }
        if (!string.IsNullOrEmpty(priority))
            filtered = filtered.Where(t => t.Priority == priority);
        if (!string.IsNullOrEmpty(assignee))
        {
            if (assignee == "unassigned")
            {
                filtered = filtered.Where(t => t.AssigneeId == null);
            }
            else if (int.TryParse(assignee, out var assigneeId))
            {
                filtered = filtered.Where(t => t.AssigneeId == assigneeId);
            }
        }
        if (dateFrom.HasValue)
            filtered = filtered.Where(t => t.CreatedAt.Date >= dateFrom.Value.Date);
        if (dateTo.HasValue)
            filtered = filtered.Where(t => t.CreatedAt.Date <= dateTo.Value.Date);

        var vm = new AdminDashboardViewModel
        {
            Tickets = filtered.ToList(),
            StatusFilter = status,
            DepartmentFilter = department,
            Search = search,
            PriorityFilter = priority,
            AssigneeFilter = assignee,
            DateFrom = dateFrom,
            DateTo = dateTo,
            TotalCount = allTickets.Count,
            OpenCount = allTickets.Count(t => t.Status == "Open"),
            InProgressCount = allTickets.Count(t => t.Status == "InProgress"),
            ResolvedCount = allTickets.Count(t => t.Status == "Resolved"),
            UnassignedCount = allTickets.Count(t => t.AssigneeId == null),
            Employees = employees,
            SuggestedAssignees = suggestedAssignees,
            EmployeeActiveTicketCounts = activeTicketCounts
        };

        return View(vm);
    }

    private static bool ContainsIgnoreCase(string? value, string term)
        => !string.IsNullOrWhiteSpace(value) &&
           value.Contains(term, StringComparison.OrdinalIgnoreCase);

    private static Dictionary<int, int> GetActiveTicketCounts(List<Ticket> tickets, List<User> employees)
        => employees.ToDictionary(
            e => e.Id,
            e => tickets.Count(t => t.AssigneeId == e.Id && t.Status is "Open" or "InProgress"));

    private static User? FindBestAssignee(
        Ticket ticket,
        List<User> employees,
        Dictionary<int, int> activeTicketCounts)
    {
        return employees
            .OrderByDescending(e => string.Equals(e.Department, ticket.Department, StringComparison.OrdinalIgnoreCase))
            .ThenBy(e => activeTicketCounts.GetValueOrDefault(e.Id))
            .ThenBy(e => e.FullName)
            .FirstOrDefault();
    }

    // POST: /Admin/UpdateStatus
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, string status)
    {
        var validStatuses = new[] { "Open", "InProgress", "Resolved" };
        if (!validStatuses.Contains(status))
        {
            TempData["Error"] = "Geçersiz durum değeri.";
            return RedirectToAction("Dashboard");
        }

        var ticket = await _ticketRepo.GetByIdAsync(id);
        if (ticket == null)
        {
            TempData["Error"] = "Ticket bulunamadı.";
            return RedirectToAction("Dashboard");
        }

        var now = DateTime.UtcNow;
        ticket.Status = status;
        ticket.UpdatedAt = now;

        // Durum geçişine göre timestamp kaydet
        if (status == "InProgress" && ticket.InProgressAt == null)
            ticket.InProgressAt = now;
        else if (status == "Resolved" && ticket.ResolvedAt == null)
            ticket.ResolvedAt = now;
        else if (status == "Open")
        {
            // Geri alındıysa ilgili timestampları temizle
            ticket.InProgressAt = null;
            ticket.ResolvedAt = null;
        }

        await _ticketRepo.UpdateAsync(ticket);

        TempData["Success"] = $"#{id} numaralı ticket durumu güncellendi: {AdminDashboardViewModel.StatusOptions.GetValueOrDefault(status, status)}";
        return RedirectToAction("Dashboard");
    }

    // POST: /Admin/AssignTicket
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignTicket(int ticketId, int employeeId)
    {
        var ticket = await _ticketRepo.GetByIdAsync(ticketId);
        if (ticket == null)
        {
            TempData["Error"] = "Ticket bulunamadı.";
            return RedirectToAction("Dashboard");
        }

        var employee = await _userRepo.GetByIdAsync(employeeId);
        if (employee == null || employee.Role != "Employee")
        {
            TempData["Error"] = "Geçersiz çalışan.";
            return RedirectToAction("Dashboard");
        }

        AssignTicketToEmployee(ticket, employee);

        // Atanınca status Open ise InProgress'e çekme (isteğe bağlı — şimdilik Open kalır)
        await _ticketRepo.UpdateAsync(ticket);

        TempData["Success"] = $"#{ticketId} numaralı ticket '{employee.FullName}' adlı çalışana atandı.";
        return RedirectToAction("Details", "Ticket", new { id = ticketId });
    }

    // POST: /Admin/QuickAssignTicket
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> QuickAssignTicket(int ticketId)
    {
        var ticket = await _ticketRepo.GetByIdAsync(ticketId);
        if (ticket == null)
        {
            TempData["Error"] = "Ticket bulunamadı.";
            return RedirectToAction("Dashboard");
        }

        if (ticket.AssigneeId.HasValue)
        {
            TempData["Error"] = "Bu ticket zaten bir çalışana atanmış.";
            return RedirectToAction("Dashboard");
        }

        var allTickets = (await _ticketRepo.GetAllAsync()).ToList();
        var employees = (await _userRepo.GetByRoleAsync("Employee")).ToList();
        var activeTicketCounts = GetActiveTicketCounts(allTickets, employees);
        var employee = FindBestAssignee(ticket, employees, activeTicketCounts);
        if (employee == null)
        {
            TempData["Error"] = "Atama yapılabilecek çalışan bulunamadı.";
            return RedirectToAction("Dashboard");
        }

        AssignTicketToEmployee(ticket, employee);
        await _ticketRepo.UpdateAsync(ticket);

        TempData["Success"] = $"#{ticketId} numaralı ticket hızlı atama ile '{employee.FullName}' adlı çalışana atandı.";
        return RedirectToAction("Dashboard");
    }

    // POST: /Admin/UnassignTicket
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UnassignTicket(int ticketId)
    {
        var ticket = await _ticketRepo.GetByIdAsync(ticketId);
        if (ticket == null)
        {
            TempData["Error"] = "Ticket bulunamadı.";
            return RedirectToAction("Dashboard");
        }

        ticket.AssigneeId = null;
        ticket.AssigneeEmail = null;
        ticket.AssigneeFullName = null;
        ticket.AssigneeAvatarUrl = null;
        ticket.AssignedAt = null;
        ticket.UpdatedAt = DateTime.UtcNow;

        await _ticketRepo.UpdateAsync(ticket);

        TempData["Success"] = $"#{ticketId} numaralı ticket ataması kaldırıldı.";
        return RedirectToAction("Details", "Ticket", new { id = ticketId });
    }

    private void AssignTicketToEmployee(Ticket ticket, User employee)
    {
        var now = DateTime.UtcNow;
        ticket.AssigneeId = employee.Id;
        ticket.AssigneeEmail = employee.Email;
        ticket.AssigneeFullName = employee.FullName;
        ticket.AssigneeAvatarUrl = employee.AvatarUrl;
        ticket.AssignedById = GetCurrentUserId();
        ticket.AssignedByEmail = GetCurrentUserEmail();
        ticket.AssignedByFullName = GetCurrentUserFullName();
        ticket.AssignedAt = now;
        ticket.UpdatedAt = now;
    }

    // POST: /Admin/Delete
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await _ticketRepo.DeleteAsync(id);
        TempData[deleted ? "Success" : "Error"] = deleted
            ? $"#{id} numaralı ticket başarıyla silindi."
            : "Ticket bulunamadı veya silinemedi.";

        return RedirectToAction("Dashboard");
    }
}
