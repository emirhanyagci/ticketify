using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ticketify.Repositories;
using Ticketify.ViewModels;

namespace Ticketify.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly ITicketRepository _ticketRepo;

    public AdminController(ITicketRepository ticketRepo)
    {
        _ticketRepo = ticketRepo;
    }

    // GET: /Admin/Dashboard
    public async Task<IActionResult> Dashboard(string? status = null)
    {
        var allTickets = (await _ticketRepo.GetAllAsync()).ToList();

        var filtered = string.IsNullOrEmpty(status)
            ? allTickets
            : allTickets.Where(t => t.Status == status).ToList();

        var vm = new AdminDashboardViewModel
        {
            Tickets = filtered,
            StatusFilter = status,
            TotalCount = allTickets.Count,
            OpenCount = allTickets.Count(t => t.Status == "Open"),
            InProgressCount = allTickets.Count(t => t.Status == "InProgress"),
            ResolvedCount = allTickets.Count(t => t.Status == "Resolved")
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

        ticket.Status = status;
        await _ticketRepo.UpdateAsync(ticket);

        TempData["Success"] = $"#{id} numaralı ticket durumu güncellendi: {AdminDashboardViewModel.StatusOptions.GetValueOrDefault(status, status)}";
        return RedirectToAction("Dashboard");
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
