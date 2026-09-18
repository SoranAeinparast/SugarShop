using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SugarShop.Domain.Entities;
using SugarShop.Infrastructure.Persistence.Sales;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
using System;
using System.IO;

namespace SugarShop.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,Owner")]
    [Route("Admin/[controller]")]
    public class ThemeController : Controller
    {
        private readonly SugarShopSalesDbContext _context;
        private readonly ILogger<ThemeController> _logger;

        public ThemeController(SugarShopSalesDbContext context, ILogger<ThemeController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            _context.ChangeTracker.Clear();
            var setting = await _context.ThemeSettings
                .OrderByDescending(t => t.Id)
                .FirstOrDefaultAsync();
            if (setting == null)
            {
                setting = new ThemeSetting();
                _context.ThemeSettings.Add(setting);
                await _context.SaveChangesAsync();
            }

            // ✅ دریافت تمام تنظیمات پایه
            var siteSetting = await _context.SiteSettings.FirstOrDefaultAsync();
            ViewBag.SiteTitle = siteSetting?.SiteTitle ?? "";
            ViewBag.SiteDescription = siteSetting?.SiteDescription ?? "";
            ViewBag.LogoPath = siteSetting?.LogoPath ?? "";
            ViewBag.FaviconPath = siteSetting?.FaviconPath ?? "";
            ViewBag.Phone = siteSetting?.Phone ?? "";
            ViewBag.Email = siteSetting?.Email ?? "";
            ViewBag.Address = siteSetting?.Address ?? "";
            ViewBag.WorkingHours = siteSetting?.WorkingHours ?? "";
            ViewBag.InstagramUrl = siteSetting?.InstagramUrl ?? "";
            ViewBag.TelegramUrl = siteSetting?.TelegramUrl ?? "";
            ViewBag.WhatsAppUrl = siteSetting?.WhatsAppUrl ?? "";
            ViewBag.BaleUrl = siteSetting?.baleUrl ?? "";
            ViewBag.RubikaUrl = siteSetting?.rubikaUrl ?? "";
            ViewBag.EitaaUrl = siteSetting?.eitaaUrl ?? "";
            ViewBag.SoroushUrl = siteSetting?.soroushUrl ?? "";
            ViewBag.CertificationsJson = siteSetting?.CertificationsJson ?? "[]";

            // تنظیمات اپلیکیشن (از ThemeSetting)
            ViewBag.AppEnabled = setting.AppEnabled;
            ViewBag.AppDisplayName = setting.AppDisplayName ?? "";
            ViewBag.AppBaseUrl = setting.AppBaseUrl ?? "";
            ViewBag.AppAndroidEnabled = setting.AppAndroidEnabled;
            ViewBag.AppIosEnabled = setting.AppIosEnabled;

            return View(setting);
        }

        [HttpPost("SaveJson")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveJson(
            [FromForm] ThemeSetting model,
            string? LogoPath,
            string? FaviconPath,
            string SiteTitle,
            string SiteDescription,
            string? Phone,
            string? Email,
            string? Address,
            string? WorkingHours,
            string? InstagramUrl,
            string? TelegramUrl,
            string? WhatsAppUrl,
            string? BaleUrl,
            string? RubikaUrl,
            string? EitaaUrl,
            string? SoroushUrl,
            string? CertificationsJson,
            bool AppEnabled,
            string? AppDisplayName,
            string? AppBaseUrl,
            bool AppAndroidEnabled,
            bool AppIosEnabled)
        {
            if (model == null)
                return BadRequest(new { success = false, message = "داده ارسال نشده است" });

            try
            {
                // ۱. به‌روزرسانی تنظیمات ظاهری
                // ✅ دقیقاً همان ردیفی که در صفحه ویرایش نشان داده می‌شود (جدیدترین رکورد)
                var setting = await _context.ThemeSettings
                    .OrderByDescending(t => t.Id)
                    .FirstOrDefaultAsync();
                if (setting == null)
                {
                    setting = new ThemeSetting();
                    _context.ThemeSettings.Add(setting);
                }
                setting.PrimaryColor = model.PrimaryColor;
                setting.SecondaryColor = model.SecondaryColor;
                setting.HeaderBgColor = model.HeaderBgColor;
                setting.HeaderTextColor = model.HeaderTextColor;
                setting.FooterBgColor = model.FooterBgColor;
                setting.FooterTextColor = model.FooterTextColor;
                setting.BodyBgColor = model.BodyBgColor;
                setting.BodyTextColor = model.BodyTextColor;
                setting.HeaderTransparency = model.HeaderTransparency;
                setting.FooterTransparency = model.FooterTransparency;
                setting.HeaderType = model.HeaderType;
                setting.FooterType = model.FooterType;
                setting.BodyDotPatternEnabled = model.BodyDotPatternEnabled;
                setting.UpdatedAt = DateTime.UtcNow;

                // ۱.۵ به‌روزرسانی تنظیمات اپلیکیشن
                setting.AppEnabled = AppEnabled;
                setting.AppDisplayName = string.IsNullOrWhiteSpace(AppDisplayName) ? null : AppDisplayName.Trim();
                setting.AppBaseUrl = string.IsNullOrWhiteSpace(AppBaseUrl) ? null : AppBaseUrl.Trim();
                if (setting.AppBaseUrl != null && !setting.AppBaseUrl.StartsWith("http"))
                    setting.AppBaseUrl = "https://" + setting.AppBaseUrl; // ورودی بدون اسکیما هم قبول است
                setting.AppBaseUrl = setting.AppBaseUrl?.TrimEnd('/');
                setting.AppAndroidEnabled = AppAndroidEnabled;
                setting.AppIosEnabled = AppIosEnabled;

                // ۲. به‌روزرسانی تنظیمات پایه
                var siteSetting = await _context.SiteSettings.FirstOrDefaultAsync() ?? new SiteSetting();
                if (!string.IsNullOrEmpty(SiteTitle)) siteSetting.SiteTitle = SiteTitle;
                if (!string.IsNullOrEmpty(SiteDescription)) siteSetting.SiteDescription = SiteDescription;
                if (!string.IsNullOrEmpty(Phone)) siteSetting.Phone = Phone;
                if (!string.IsNullOrEmpty(Email)) siteSetting.Email = Email;
                if (!string.IsNullOrEmpty(Address)) siteSetting.Address = Address;
                if (!string.IsNullOrEmpty(WorkingHours)) siteSetting.WorkingHours = WorkingHours;
                if (!string.IsNullOrEmpty(InstagramUrl)) siteSetting.InstagramUrl = InstagramUrl;
                if (!string.IsNullOrEmpty(TelegramUrl)) siteSetting.TelegramUrl = TelegramUrl;
                if (!string.IsNullOrEmpty(WhatsAppUrl)) siteSetting.WhatsAppUrl = WhatsAppUrl;
                if (!string.IsNullOrEmpty(BaleUrl)) siteSetting.baleUrl = BaleUrl;
                if (!string.IsNullOrEmpty(RubikaUrl)) siteSetting.rubikaUrl = RubikaUrl;
                if (!string.IsNullOrEmpty(EitaaUrl)) siteSetting.eitaaUrl = EitaaUrl;
                if (!string.IsNullOrEmpty(SoroushUrl)) siteSetting.soroushUrl = SoroushUrl;
                if (!string.IsNullOrEmpty(CertificationsJson)) siteSetting.CertificationsJson = CertificationsJson;

                if (!string.IsNullOrEmpty(LogoPath)) siteSetting.LogoPath = LogoPath;
                if (!string.IsNullOrEmpty(FaviconPath)) siteSetting.FaviconPath = FaviconPath;

                siteSetting.UpdatedAt = DateTime.UtcNow;
                if (siteSetting.Id == 0) _context.SiteSettings.Add(siteSetting);

                await _context.SaveChangesAsync();
                _logger.LogInformation("تنظیمات ظاهری و پایه با موفقیت ذخیره شدند.");
                return Ok(new { success = true, message = "تمامی تنظیمات با موفقیت ذخیره شد." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در SaveJson");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }
    }
}