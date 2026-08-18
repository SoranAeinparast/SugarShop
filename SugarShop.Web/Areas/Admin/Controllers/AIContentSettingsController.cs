using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SugarShop.Domain.Entities;
using SugarShop.Infrastructure.Persistence.Sales;
using SugarShop.Web.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SugarShop.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,Owner")]
    [Route("Admin/[controller]")]
    public class AIContentSettingsController : Controller
    {
        private readonly SugarShopSalesDbContext _context;
        private readonly ContentSchedulerService _scheduler;
        private readonly ILogger<AIContentSettingsController> _logger;

        public AIContentSettingsController(
            SugarShopSalesDbContext context,
            ContentSchedulerService scheduler,
            ILogger<AIContentSettingsController> logger)
        {
            _context = context;
            _scheduler = scheduler;
            _logger = logger;
        }
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            try
            {
                var settings = await _context.AIContentSettings.FirstOrDefaultAsync() ?? new AIContentSettings();
                var sources = await _context.ContentSources.OrderBy(s => s.Priority).ToListAsync();
                var topics = await _context.ContentTopics.OrderBy(t => t.Name).ToListAsync();

                ViewBag.Sources = sources;
                ViewBag.Topics = topics;
                return View(settings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در بارگذاری صفحه تنظیمات");
                TempData["Error"] = "خطا در بارگذاری اطلاعات. لطفاً دوباره تلاش کنید.";
                return View(new AIContentSettings());
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveSettings(AIContentSettings model)
        {
            if (model == null)
            {
                TempData["Error"] = "داده‌های ارسالی نامعتبر است.";
                return RedirectToAction(nameof(Index));
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();
                TempData["Error"] = "⚠️ خطا در اعتبارسنجی: " + string.Join("; ", errors);

                ViewBag.Sources = await _context.ContentSources.ToListAsync();
                ViewBag.Topics = await _context.ContentTopics.ToListAsync();
                return View("Index", model);
            }

            try
            {
                var settings = await _context.AIContentSettings.FirstOrDefaultAsync();
                if (settings == null)
                {
                    settings = new AIContentSettings();
                    _context.AIContentSettings.Add(settings);
                }

                // به‌روزرسانی فیلدها
                settings.OpenAIApiKey = model.OpenAIApiKey;
                settings.ModelName = model.ModelName ?? "gpt-4o-mini";
                settings.ImageModelName = model.ImageModelName ?? "dall-e-3";
                settings.AutoPublishEnabled = model.AutoPublishEnabled;
                settings.RequireAdminApproval = model.RequireAdminApproval;
                settings.ScheduleCron = model.ScheduleCron ?? "0 8 * * *";
                settings.MaxArticlesPerRun = model.MaxArticlesPerRun > 0 ? model.MaxArticlesPerRun : 5;
                settings.UpdatedAt = DateTime.UtcNow;

                var changes = await _context.SaveChangesAsync();

                if (changes > 0)
                {
                    TempData["Success"] = "✅ تنظیمات با موفقیت ذخیره شد.";
                }
                else
                {
                    TempData["Warning"] = "⚠️ هیچ تغییری اعمال نشد (مقادیر تکراری). لطفاً یک مقدار جدید وارد کنید.";
                }

                await _scheduler.ScheduleContentFetching();
                _logger.LogInformation("تنظیمات AI توسط کاربر {User} به‌روزرسانی شد", User.Identity?.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در ذخیره تنظیمات AI");
                TempData["Error"] = $"❌ خطا در ذخیره‌سازی: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }
        [HttpPost("AddSource")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddSource(ContentSource model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.Url))
            {
                TempData["Error"] = "❌ آدرس URL نمی‌تواند خالی باشد.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                model.CreatedAt = DateTime.UtcNow;
                model.IsActive = true;
                _context.ContentSources.Add(model);
                await _context.SaveChangesAsync();
                TempData["Success"] = "✅ منبع با موفقیت اضافه شد.";
                _logger.LogInformation("منبع جدید اضافه شد: {Url}", model.Url);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در افزودن منبع");
                TempData["Error"] = $"❌ خطا در افزودن منبع: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }
        [HttpGet("EditSource/{id}")]
        public async Task<IActionResult> EditSource(int id)
        {
            var source = await _context.ContentSources.FindAsync(id);
            if (source == null) return NotFound();
            return View(source);
        }
        [HttpPost("EditSource/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditSource(int id, ContentSource model)
        {
            if (id != model.Id) return NotFound();

            if (string.IsNullOrWhiteSpace(model.Url))
            {
                TempData["Error"] = "❌ آدرس URL نمی‌تواند خالی باشد.";
                return View(model);
            }

            var existing = await _context.ContentSources.FindAsync(id);
            if (existing == null) return NotFound();

            existing.Title = model.Title;
            existing.Url = model.Url;
            existing.Category = model.Category;
            existing.IsActive = model.IsActive;
            existing.Priority = model.Priority;

            await _context.SaveChangesAsync();
            TempData["Success"] = "✅ منبع با موفقیت ویرایش شد.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost("DeleteSource")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSource(int id)
        {
            try
            {
                var source = await _context.ContentSources.FindAsync(id);
                if (source != null)
                {
                    _context.ContentSources.Remove(source);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "✅ منبع با موفقیت حذف شد.";
                    _logger.LogInformation("منبع با Id {Id} حذف شد", id);
                }
                else
                {
                    TempData["Error"] = "❌ منبع یافت نشد.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در حذف منبع {Id}", id);
                TempData["Error"] = $"❌ خطا در حذف منبع: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }
        [HttpPost("AddTopic")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddTopic(ContentTopic model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.Name))
            {
                TempData["Error"] = "❌ نام موضوع نمی‌تواند خالی باشد.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                model.CreatedAt = DateTime.UtcNow;
                model.IsActive = true;
                _context.ContentTopics.Add(model);
                await _context.SaveChangesAsync();
                TempData["Success"] = "✅ موضوع با موفقیت اضافه شد.";
                _logger.LogInformation("موضوع جدید اضافه شد: {Name}", model.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در افزودن موضوع");
                TempData["Error"] = $"❌ خطا در افزودن موضوع: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost("DeleteTopic")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteTopic(int id)
        {
            try
            {
                var topic = await _context.ContentTopics.FindAsync(id);
                if (topic != null)
                {
                    _context.ContentTopics.Remove(topic);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "✅ موضوع با موفقیت حذف شد.";
                    _logger.LogInformation("موضوع با Id {Id} حذف شد", id);
                }
                else
                {
                    TempData["Error"] = "❌ موضوع یافت نشد.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در حذف موضوع {Id}", id);
                TempData["Error"] = $"❌ خطا در حذف موضوع: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}