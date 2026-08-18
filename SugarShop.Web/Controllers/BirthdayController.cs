using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SugarShop.Domain.Entities;
using SugarShop.Domain.Entities.Sales;
using SugarShop.Infrastructure.Persistence.Sales;
using System;
using System.Threading.Tasks;

namespace SugarShop.Web.Controllers
{
    [Authorize]
    public class BirthdayController : Controller
    {
        private readonly SugarShopSalesDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public BirthdayController(SugarShopSalesDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);
            var reminders = await _context.BirthdayReminders
                .Where(r => r.UserId == userId)
                .OrderBy(r => r.BirthDate)
                .ToListAsync();
            return View(reminders);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(BirthdayReminder model)
        {
            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();
            ModelState.Remove("UserId");
            ModelState.Remove("CreatedAt");

            if (ModelState.IsValid)
            {
                model.UserId = userId;
                model.CreatedAt = DateTime.UtcNow;
                _context.BirthdayReminders.Add(model);
                await _context.SaveChangesAsync();
                TempData["Success"] = "یادآوری تولد با موفقیت اضافه شد.";
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var userId = _userManager.GetUserId(User);
            var reminder = await _context.BirthdayReminders
                .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId);
            if (reminder == null) return NotFound();

            return View(reminder);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, BirthdayReminder model)
        {
            if (id != model.Id) return NotFound();

            var userId = _userManager.GetUserId(User);
            var reminder = await _context.BirthdayReminders
                .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId);
            if (reminder == null) return NotFound();
            ModelState.Remove("UserId");
            ModelState.Remove("CreatedAt");

            if (ModelState.IsValid)
            {
                reminder.FirstName = model.FirstName;
                reminder.LastName = model.LastName;
                reminder.Gender = model.Gender;
                reminder.BirthDate = model.BirthDate;
                reminder.Relation = model.Relation;
                reminder.Email = model.Email;
                reminder.PhoneNumber = model.PhoneNumber;
                reminder.Notes = model.Notes;
                reminder.RemindDaysBefore = model.RemindDaysBefore;
                reminder.IsActive = model.IsActive;
                reminder.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                TempData["Success"] = "یادآوری با موفقیت به‌روزرسانی شد.";
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = _userManager.GetUserId(User);
            var reminder = await _context.BirthdayReminders
                .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId);
            if (reminder != null)
            {
                _context.BirthdayReminders.Remove(reminder);
                await _context.SaveChangesAsync();
                TempData["Success"] = "یادآوری حذف شد.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}