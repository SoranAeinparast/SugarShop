using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SugarShop.Domain.Entities;
using SugarShop.Infrastructure.Persistence.Sales;
using SugarShop.Web.Services;
using SugarShop.Web.Services.Interfaces;
using System;
using System.Linq;
using System.Text.RegularExpressions;
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
        private readonly IAIContentService _aiContentService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AIContentSettingsController> _logger;

        public AIContentSettingsController(
            SugarShopSalesDbContext context,
            ContentSchedulerService scheduler,
            IAIContentService aiContentService,
            IConfiguration configuration,
            ILogger<AIContentSettingsController> logger)
        {
            _context = context;
            _scheduler = scheduler;
            _aiContentService = aiContentService;
            _configuration = configuration;
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
                ViewBag.TextAiConfigured = await _aiContentService.IsTextAiConfiguredAsync();
                ViewBag.AgnesKeyConfigured = !string.IsNullOrWhiteSpace(
                    _configuration?.GetSection("AgnesAI:ApiKey").Value);
                ViewBag.InvalidCron = !string.IsNullOrWhiteSpace(settings.ScheduleCron)
                    && !TryValidateCron(settings.ScheduleCron.Trim(), out _);
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
                ViewBag.TextAiConfigured = await _aiContentService.IsTextAiConfiguredAsync();
                ViewBag.AgnesKeyConfigured = !string.IsNullOrWhiteSpace(
                    _configuration?.GetSection("AgnesAI:ApiKey").Value);
                return View("Index", model);
            }

            // اعتبارسنجی عبارت cron پیش از ذخیره تا مقدار نامعتبر وارد دیتابیس نشود
            // (cron استاندارد ۵ بخش دارد: دقیقه ساعت روز-ماه ماه روز-هفته)
            var cron = string.IsNullOrWhiteSpace(model.ScheduleCron) ? "0 8 * * *" : model.ScheduleCron.Trim();
            if (!TryValidateCron(cron, out var cronHint))
            {
                TempData["Error"] = "❌ عبارت Cron نامعتبر است: " + cronHint;

                ViewBag.Sources = await _context.ContentSources.ToListAsync();
                ViewBag.Topics = await _context.ContentTopics.ToListAsync();
                ViewBag.TextAiConfigured = await _aiContentService.IsTextAiConfiguredAsync();
                ViewBag.AgnesKeyConfigured = !string.IsNullOrWhiteSpace(
                    _configuration?.GetSection("AgnesAI:ApiKey").Value);
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
                // کلید فقط در صورتی بازنویسی می‌شود که مقدار جدیدی وارد شده باشد؛
                // در غیر این صورت کلید ذخیره‌شده قبلی حفظ می‌شود (فیلد کلید هنگام نمایش خالی است).
                if (!string.IsNullOrWhiteSpace(model.OpenAIApiKey))
                {
                    settings.OpenAIApiKey = model.OpenAIApiKey.Trim();
                }
                settings.ModelName = string.IsNullOrWhiteSpace(model.ModelName) ? settings.ModelName ?? "gpt-4o-mini" : model.ModelName.Trim();
                settings.ImageModelName = string.IsNullOrWhiteSpace(model.ImageModelName) ? settings.ImageModelName ?? "dall-e-3" : model.ImageModelName.Trim();
                settings.AutoPublishEnabled = model.AutoPublishEnabled;
                settings.RequireAdminApproval = model.RequireAdminApproval;
                settings.ScheduleCron = cron;
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

        /// <summary>
        /// بررسی سبک فرمت cron (همان فرمتی که Hangfire می‌پذیرد): ۵ یا ۶ بخش با فاصله.
        /// فقط خطاهای پرتکرار (تعداد بخش‌ها، اعداد فارسی یا کاراکتر نامعتبر) پیش از ذخیره گرفته می‌شوند؛
        /// اعتبارسنجی نهایی معادل Hangfire هنگام زمان‌بندی انجام و در صورت لزوم به پیش‌فرض برمی‌گردد.
        /// </summary>
        private static bool TryValidateCron(string cron, out string hint)
        {
            hint = string.Empty;
            var parts = cron.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length is < 5 or > 6)
            {
                hint = parts.Length < 5
                    ? $"بخش‌های واردشده {parts.Length} عدد است اما cron استاندارد ۵ بخش دارد: دقیقه، ساعت، روز ماه، ماه، روز هفته. برای «دقیقهٔ ۲۳ هر ساعت» باید بنویسید: 23 * * * * (در پایان شما یک * کم دارید)."
                    : $"بخش‌های واردشده {parts.Length} عدد است؛ فرمت ۵ بخش (یا ۶ بخش با ثانیه) پذیرفته می‌شود.";
                return false;
            }

            foreach (var part in parts)
            {
                if (string.IsNullOrEmpty(part))
                {
                    hint = "بخشی از عبارت خالی است.";
                    return false;
                }
                if (part.Any(ch => ch >= '۰' && ch <= '۹'))
                {
                    hint = "از اعداد انگلیسی استفاده کنید؛ اعداد فارسی در عبارت cron پشتیبانی نمی‌شوند.";
                    return false;
                }
                if (!Regex.IsMatch(part, @"^[0-9A-Za-z*,/#?LW-]+$"))
                {
                    hint = $"بخش «{part}» شامل کاراکتر نامعتبر است.";
                    return false;
                }
            }

            return true;
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
        [HttpPost("TestConnection")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TestConnection()
        {
            try
            {
                var result = await _aiContentService.TestConnectionAsync();
                return Json(new
                {
                    success = result.Success,
                    provider = result.Provider,
                    message = result.Message,
                    latencyMs = result.LatencyMs
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در تست اتصال AI");
                return Json(new { success = false, provider = "-", message = $"خطای غیرمنتظره: {ex.Message}", latencyMs = 0 });
            }
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