using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
    public async Task<IActionResult> Dashboard(string? status = null, string? department = null)
    {
        var allTickets = (await _ticketRepo.GetAllAsync()).ToList();
        var employees = (await _userRepo.GetByRoleAsync("Employee")).ToList();

        var filtered = allTickets.AsEnumerable();
        if (!string.IsNullOrEmpty(status))
            filtered = filtered.Where(t => t.Status == status);
        if (!string.IsNullOrEmpty(department))
            filtered = filtered.Where(t => t.Department == department);

        var vm = new AdminDashboardViewModel
        {
            Tickets = filtered.ToList(),
            StatusFilter = status,
            DepartmentFilter = department,
            TotalCount = allTickets.Count,
            OpenCount = allTickets.Count(t => t.Status == "Open"),
            InProgressCount = allTickets.Count(t => t.Status == "InProgress"),
            ResolvedCount = allTickets.Count(t => t.Status == "Resolved"),
            UnassignedCount = allTickets.Count(t => t.AssigneeId == null),
            Employees = employees
        };

        return View(vm);
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

        // Atanınca status Open ise InProgress'e çekme (isteğe bağlı — şimdilik Open kalır)
        await _ticketRepo.UpdateAsync(ticket);

        TempData["Success"] = $"#{ticketId} numaralı ticket '{employee.FullName}' adlı çalışana atandı.";
        return RedirectToAction("Details", "Ticket", new { id = ticketId });
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
