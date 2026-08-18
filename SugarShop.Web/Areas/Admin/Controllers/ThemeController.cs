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

            // ✅ دریافت مقادیر فعلی تنظیمات پایه برای نمایش در ویو
            var siteSetting = await _context.SiteSettings.FirstOrDefaultAsync();
            ViewBag.SiteTitle = siteSetting?.SiteTitle ?? "";
            ViewBag.SiteDescription = siteSetting?.SiteDescription ?? "";
            ViewBag.LogoPath = siteSetting?.LogoPath ?? "";
            ViewBag.FaviconPath = siteSetting?.FaviconPath ?? "";

            return View(setting);
        }

        [HttpPost("SaveJson")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveJson(
            [FromForm] ThemeSetting model, // ✅ تغییر به FromForm برای پشتیبانی از FormData
            string? LogoPath,              // ✅ مسیر لوگو (انتخاب‌شده از Media Picker)
            string? FaviconPath,           // ✅ مسیر فاوآیکون (انتخاب‌شده از Media Picker)
            string SiteTitle,              // ✅ دریافت عنوان سایت
            string SiteDescription)        // ✅ دریافت توضیحات متا
        {
            if (model == null)
                return BadRequest(new { success = false, message = "داده ارسال نشده است" });

            try
            {
                // ۱. به‌روزرسانی تنظیمات ظاهری
                var setting = await _context.ThemeSettings.FirstOrDefaultAsync();
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

                // ۲. به‌روزرسانی تنظیمات پایه (SiteSettings)
                var siteSetting = await _context.SiteSettings.FirstOrDefaultAsync() ?? new SiteSetting();

                if (!string.IsNullOrEmpty(SiteTitle)) siteSetting.SiteTitle = SiteTitle;
                if (!string.IsNullOrEmpty(SiteDescription)) siteSetting.SiteDescription = SiteDescription;

                // ذخیره مسیر لوگو (انتخاب‌شده از Media Picker)
                if (!string.IsNullOrEmpty(LogoPath))
                {
                    siteSetting.LogoPath = LogoPath;
                }

                // ذخیره مسیر فاوآیکون (انتخاب‌شده از Media Picker)
                if (!string.IsNullOrEmpty(FaviconPath))
                {
                    siteSetting.FaviconPath = FaviconPath;
                }

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