using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ticketify.Models;
using Ticketify.Repositories;
using Ticketify.Services;
using Ticketify.ViewModels;

namespace Ticketify.Controllers;

[Authorize(Roles = "Admin")]
public class EmployeeController : Controller
{
    private readonly IUserRepository _userRepo;
    private readonly ITicketRepository _ticketRepo;
    private readonly AuthService _authService;
    private readonly AvatarService _avatarService;

    public EmployeeController(
        IUserRepository userRepo,
        ITicketRepository ticketRepo,
        AuthService authService,
        AvatarService avatarService)
    {
        _userRepo = userRepo;
        _ticketRepo = ticketRepo;
        _authService = authService;
        _avatarService = avatarService;
    }

    // GET: /Employee
    public async Task<IActionResult> Index(string? department = null)
    {
        var allEmployees = (await _userRepo.GetByRoleAsync("Employee")).ToList();

        var filtered = string.IsNullOrEmpty(department)
            ? allEmployees
            : allEmployees.Where(e => e.Department == department).ToList();

        var vm = new EmployeeListViewModel
        {
            Employees = filtered,
            DepartmentFilter = department,
            TotalCount = allEmployees.Count
        };

        return View(vm);
    }

    // GET: /Employee/Create
    public IActionResult Create()
    {
        return View(new EmployeeCreateViewModel());
    }

    // POST: /Employee/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(EmployeeCreateViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var existing = await _userRepo.GetByEmailAsync(model.Email);
        if (existing != null)
        {
            ModelState.AddModelError("Email", "Bu e-posta adresi zaten kayıtlı.");
            return View(model);
        }

        var employee = new User
        {
            FullName = model.FullName,
            Email = model.Email,
            PasswordHash = _authService.HashPassword(model.Password),
            Role = "Employee",
            Department = model.Department,
            AvatarUrl = _avatarService.GenerateAvatarUrl(model.Email)
        };

        await _userRepo.CreateAsync(employee);

        TempData["Success"] = $"'{model.FullName}' adlı çalışan başarıyla eklendi.";
        return RedirectToAction("Index");
    }

    // GET: /Employee/Edit/{id}
    public async Task<IActionResult> Edit(int id)
    {
        var employee = await _userRepo.GetByIdAsync(id);
        if (employee == null || employee.Role != "Employee")
        {
            TempData["Error"] = "Çalışan bulunamadı.";
            return RedirectToAction("Index");
        }

        var vm = new EmployeeEditViewModel
        {
            Id = employee.Id,
            FullName = employee.FullName,
            Email = employee.Email,
            Department = employee.Department ?? "General"
        };

        return View(vm);
    }

    // POST: /Employee/Edit
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EmployeeEditViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var employee = await _userRepo.GetByIdAsync(model.Id);
        if (employee == null || employee.Role != "Employee")
        {
            TempData["Error"] = "Çalışan bulunamadı.";
            return RedirectToAction("Index");
        }

        // E-posta değiştiyse duplicate kontrol
        if (!employee.Email.Equals(model.Email, StringComparison.OrdinalIgnoreCase))
        {
            var existing = await _userRepo.GetByEmailAsync(model.Email);
            if (existing != null)
            {
                ModelState.AddModelError("Email", "Bu e-posta adresi zaten kayıtlı.");
                return View(model);
            }
        }

        employee.FullName = model.FullName;
        employee.Email = model.Email;
        employee.Department = model.Department;
        employee.AvatarUrl = _avatarService.GenerateAvatarUrl(model.Email);

        // Şifre değişikliği isteğe bağlı
        if (!string.IsNullOrEmpty(model.NewPassword))
            employee.PasswordHash = _authService.HashPassword(model.NewPassword);

        await _userRepo.UpdateAsync(employee);

        TempData["Success"] = $"'{employee.FullName}' adlı çalışan bilgileri güncellendi.";
        return RedirectToAction("Index");
    }

    // POST: /Employee/Delete
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var employee = await _userRepo.GetByIdAsync(id);
        if (employee == null || employee.Role != "Employee")
        {
            TempData["Error"] = "Çalışan bulunamadı.";
            return RedirectToAction("Index");
        }

        var name = employee.FullName;
        var deleted = await _userRepo.DeleteAsync(id);

        TempData[deleted ? "Success" : "Error"] = deleted
            ? $"'{name}' adlı çalışan sistemden silindi."
            : "Çalışan silinemedi.";

        return RedirectToAction("Index");
    }
}
