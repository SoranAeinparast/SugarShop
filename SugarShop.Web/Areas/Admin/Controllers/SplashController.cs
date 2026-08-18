using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SugarShop.Domain.Entities;
using SugarShop.Infrastructure.Persistence.Sales;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;

namespace SugarShop.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,Owner")]
    [Route("Admin/[controller]")]
    public class SplashController : Controller
    {
        private readonly SugarShopSalesDbContext _context;
        private readonly ILogger<SplashController> _logger;

        public SplashController(SugarShopSalesDbContext context, ILogger<SplashController> logger)
        {
            _context = context;
            _logger = logger;
        }
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var setting = await _context.SplashSettings.FirstOrDefaultAsync();
            if (setting == null)
            {
                setting = new SplashSetting
                {
                    VideoPath = "",
                    ButtonText = "ورود به سایت",
                    IsEnabled = false,
                    IsMuted = true
                };
                _context.SplashSettings.Add(setting);
                await _context.SaveChangesAsync();
            }
            return View(setting);
        }
        [HttpPost("SaveJson")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveJson([FromBody] SplashSetting model)
        {
            if (model == null)
                return BadRequest(new { success = false, message = "داده ارسال نشده است" });

            try
            {
                var setting = await _context.SplashSettings.FirstOrDefaultAsync();
                if (setting == null)
                {
                    setting = new SplashSetting();
                    _context.SplashSettings.Add(setting);
                }

                setting.ButtonText = model.ButtonText ?? "ورود";
                setting.IsEnabled = model.IsEnabled;
                setting.IsMuted = model.IsMuted;
                setting.UpdatedAt = DateTime.UtcNow;

                if (!string.IsNullOrEmpty(model.VideoPath))
                {
                    setting.VideoPath = model.VideoPath;
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation("تنظیمات اسپلش ذخیره شد: ButtonText={ButtonText}, IsEnabled={IsEnabled}, IsMuted={IsMuted}",
                    setting.ButtonText, setting.IsEnabled, setting.IsMuted);

                return Ok(new { success = true, message = "تنظیمات اسپلش با موفقیت ذخیره شد." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در SaveJson");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }
        [HttpPost("UploadVideo")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadVideo(
            string? videoPath,
            string? buttonText,
            bool? isEnabled,
            bool? isMuted)
        {
            try
            {
                var settings = await _context.SplashSettings.FirstOrDefaultAsync();
                if (settings == null)
                {
                    settings = new SplashSetting();
                    _context.SplashSettings.Add(settings);
                }
                if (!string.IsNullOrWhiteSpace(videoPath))
                {
                    settings.VideoPath = videoPath;
                }
                if (buttonText != null)
                    settings.ButtonText = buttonText;
                if (isEnabled.HasValue)
                    settings.IsEnabled = isEnabled.Value;
                if (isMuted.HasValue) 
                    settings.IsMuted = isMuted.Value;

                settings.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                return Ok(new { success = true, message = "ویدئو با موفقیت آپلود شد." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در آپلود ویدئو");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }
        [HttpPost("DeleteVideo")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteVideo()
        {
            try
            {
                var settings = await _context.SplashSettings.FirstOrDefaultAsync();
                if (settings != null && !string.IsNullOrEmpty(settings.VideoPath))
                {
                    settings.VideoPath = "";
                    settings.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                    return Ok(new { success = true, message = "ویدئو با موفقیت حذف شد." });
                }
                return BadRequest(new { success = false, message = "ویدئویی برای حذف وجود ندارد." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در حذف ویدئو");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }
    }
}