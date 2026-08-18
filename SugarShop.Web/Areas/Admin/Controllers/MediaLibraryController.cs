using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SugarShop.Domain.Entities;
using SugarShop.Infrastructure.Persistence.Sales;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SugarShop.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,Owner")]
    [Route("Admin/MediaLibrary")] // مسیر پایه کنترلر
    public class MediaLibraryController : Controller
    {
        private readonly SugarShopSalesDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<MediaLibraryController> _logger;

        private const long MaxImageSize = 5 * 1024 * 1024;
        private const long MaxVideoSize = 100 * 1024 * 1024;
        private const long MaxDocumentSize = 10 * 1024 * 1024;
        private const long MaxAudioSize = 20 * 1024 * 1024;

        private static readonly string[] AllowedImageExts = { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".svg" };
        private static readonly string[] AllowedVideoExts = { ".mp4", ".webm", ".mov" };
        private static readonly string[] AllowedDocumentExts = { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".txt", ".zip" };
        private static readonly string[] AllowedAudioExts = { ".mp3", ".wav", ".ogg", ".m4a" };

        public MediaLibraryController(SugarShopSalesDbContext context, IWebHostEnvironment env, ILogger<MediaLibraryController> logger)
        {
            _context = context;
            _env = env;
            _logger = logger;
        }

        [HttpGet]
        [Route("")] // دقیقاً منطبق بر /Admin/MediaLibrary
        public async Task<IActionResult> Index(string? search, string? category, string? fileType, int page = 1)
        {
            var query = _context.MediaAssets.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower();
                query = query.Where(m => m.OriginalName.ToLower().Contains(s) || m.Tags.ToLower().Contains(s) || m.Description.ToLower().Contains(s) || m.AltText.ToLower().Contains(s));
            }

            if (!string.IsNullOrWhiteSpace(category) && category != "All")
                query = query.Where(m => m.Category == category);

            if (!string.IsNullOrWhiteSpace(fileType) && fileType != "All")
                query = query.Where(m => m.FileType == fileType);

            var totalItems = await query.CountAsync();
            var assets = await query.OrderByDescending(m => m.CreatedAt).Skip((page - 1) * 20).Take(20).ToListAsync();

            ViewBag.Search = search;
            ViewBag.Category = category ?? "All";
            ViewBag.FileType = fileType ?? "All";
            ViewBag.TotalItems = totalItems;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalItems / 20.0);
            ViewBag.Categories = new[] { "All", "General", "Products", "Banners", "Sliders", "AboutUs", "Blog", "Gallery" };
            ViewBag.FileTypes = new[] { "All", "Image", "Video", "Document", "Audio" };

            return View(assets);
        }

        [HttpPost]
        [Route("Upload")] // دقیقاً منطبق بر /Admin/MediaLibrary/Upload
        [RequestSizeLimit(120 * 1024 * 1024)]
        public async Task<IActionResult> Upload(IFormFile file, string category = "General", string tags = "", string altText = "", string description = "")
        {
            if (file == null || file.Length == 0) return Json(new { success = false, message = "فایلی انتخاب نشده است." });

            try
            {
                var ext = Path.GetExtension(file.FileName).ToLower();
                var fileType = GetFileType(ext);
                var maxSize = GetMaxSize(fileType);

                if (!IsExtensionAllowed(ext, fileType)) return Json(new { success = false, message = $"پسوند {ext} مجاز نیست." });
                if (file.Length > maxSize) return Json(new { success = false, message = $"حجم فایل بیشتر از حد مجاز ({maxSize / (1024 * 1024)} مگابایت) است." });

                var year = DateTime.UtcNow.Year.ToString();
                var month = DateTime.UtcNow.Month.ToString("D2");
                var folder = Path.Combine(_env.WebRootPath, "media", year, month);
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                var uniqueName = $"{Guid.NewGuid():N}{ext}";
                var filePath = Path.Combine(folder, uniqueName);

                using (var stream = new FileStream(filePath, FileMode.Create)) await file.CopyToAsync(stream);

                var asset = new MediaAsset
                {
                    FileName = uniqueName,
                    OriginalName = file.FileName,
                    FilePath = $"/media/{year}/{month}/{uniqueName}",
                    FileType = fileType,
                    MimeType = file.ContentType,
                    Extension = ext,
                    FileSize = file.Length,
                    Category = string.IsNullOrWhiteSpace(category) ? "General" : category,
                    Tags = tags ?? "",
                    AltText = altText ?? "",
                    Description = description ?? "",
                    UsageCount = 0,
                    CreatedAt = DateTime.UtcNow
                };

                _context.MediaAssets.Add(asset);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "فایل با موفقیت آپلود شد." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در آپلود");
                return Json(new { success = false, message = $"خطا: {ex.Message}" });
            }
        }

        [HttpPost]
        [Route("Edit")] // دقیقاً منطبق بر /Admin/MediaLibrary/Edit
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, string category, string tags, string altText, string description)
        {
            var asset = await _context.MediaAssets.FindAsync(id);
            if (asset == null)
            {
                TempData["Error"] = "فایل مورد نظر یافت نشد.";
                return RedirectToAction(nameof(Index));
            }

            asset.Category = string.IsNullOrWhiteSpace(category) ? "General" : category;
            asset.Tags = tags ?? "";
            asset.AltText = altText ?? "";
            asset.Description = description ?? "";
            asset.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            TempData["Success"] = "اطلاعات فایل با موفقیت به‌روزرسانی شد.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [Route("Delete")] // دقیقاً منطبق بر /Admin/MediaLibrary/Delete
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var asset = await _context.MediaAssets.FindAsync(id);
            if (asset == null)
            {
                TempData["Error"] = "فایل مورد نظر یافت نشد.";
                return RedirectToAction(nameof(Index));
            }

            if (asset.UsageCount > 0)
            {
                TempData["Error"] = $"این فایل در {asset.UsageCount} مکان استفاده شده و قابل حذف نیست.";
                return RedirectToAction(nameof(Index));
            }

            var physicalPath = Path.Combine(_env.WebRootPath, asset.FilePath.TrimStart('/'));
            if (System.IO.File.Exists(physicalPath))
            {
                try { System.IO.File.Delete(physicalPath); }
                catch { }
            }

            _context.MediaAssets.Remove(asset);
            await _context.SaveChangesAsync();

            TempData["Success"] = "فایل با موفقیت حذف شد.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        [Route("GetAssetUrl/{id:int}")]
        public IActionResult GetAssetUrl(int id)
        {
            var asset = _context.MediaAssets.Find(id);
            if (asset == null) return Json(new { success = false, message = "فایل یافت نشد." });

            asset.UsageCount++;
            _context.SaveChanges();

            return Json(new { success = true, url = asset.FilePath, id = asset.Id });
        }

        private string GetFileType(string ext)
        {
            if (AllowedImageExts.Contains(ext)) return "Image";
            if (AllowedVideoExts.Contains(ext)) return "Video";
            if (AllowedDocumentExts.Contains(ext)) return "Document";
            if (AllowedAudioExts.Contains(ext)) return "Audio";
            return "General";
        }

        private long GetMaxSize(string fileType) => fileType switch
        {
            "Image" => MaxImageSize,
            "Video" => MaxVideoSize,
            "Document" => MaxDocumentSize,
            "Audio" => MaxAudioSize,
            _ => MaxDocumentSize
        };

        private bool IsExtensionAllowed(string ext, string fileType) => fileType switch
        {
            "Image" => AllowedImageExts.Contains(ext),
            "Video" => AllowedVideoExts.Contains(ext),
            "Document" => AllowedDocumentExts.Contains(ext),
            "Audio" => AllowedAudioExts.Contains(ext),
            _ => false
        };

        // ============================================================
        // 📂 دریافت فایل‌ها برای مودال انتخاب‌گر (سبک و سریع)
        // ============================================================
        [HttpGet("PickerData")]
        public async Task<IActionResult> PickerData(string? search, string? type)
        {
            var query = _context.MediaAssets.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower();
                query = query.Where(m => m.OriginalName.ToLower().Contains(s) || m.Tags.ToLower().Contains(s));
            }

            if (!string.IsNullOrWhiteSpace(type) && type != "All")
            {
                query = query.Where(m => m.FileType == type);
            }

            var assets = await query
                .OrderByDescending(m => m.CreatedAt)
                .Take(50) // فقط ۵۰ فایل آخر برای سرعت بالا در مودال
                .Select(m => new
                {
                    m.Id,
                    m.OriginalName,
                    m.FilePath,
                    m.FileType,
                    Thumbnail = m.FileType == "Image" ? m.FilePath : "/images/default-file.png" // می‌توانید بعداً تامنیل واقعی اضافه کنید
                })
                .ToListAsync();

            return Json(assets);
        }
    }

}