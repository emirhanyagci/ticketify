using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ticketify.Models;
using Ticketify.Repositories;
using Ticketify.ViewModels;

namespace Ticketify.Controllers;

[Authorize]
public class TicketController : Controller
{
    private readonly ITicketRepository _ticketRepo;

    public TicketController(ITicketRepository ticketRepo)
    {
        _ticketRepo = ticketRepo;
    }

    private int GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(claim, out var id) ? id : 0;
    }

    private string GetCurrentUserEmail()
    {
        return User.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty;
    }

    // GET: /Ticket/Index
    public async Task<IActionResult> Index()
    {
        var userId = GetCurrentUserId();
        var tickets = await _ticketRepo.GetByUserIdAsync(userId);

        var vm = new TicketListViewModel
        {
            Tickets = tickets,
            UserEmail = GetCurrentUserEmail()
        };

        return View(vm);
    }

    // GET: /Ticket/Create
    public IActionResult Create()
    {
        return View(new TicketCreateViewModel());
    }

    // POST: /Ticket/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TicketCreateViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var ticket = new Ticket
        {
            Title = model.Title,
            Description = model.Description,
            Priority = model.Priority,
            Status = "Open",
            UserId = GetCurrentUserId(),
            UserEmail = GetCurrentUserEmail()
        };

        await _ticketRepo.CreateAsync(ticket);

        TempData["Success"] = "Destek talebiniz başarıyla oluşturuldu!";
        return RedirectToAction("Index");
    }
}
