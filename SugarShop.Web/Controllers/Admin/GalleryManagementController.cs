using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SugarShop.Domain.Entities;
using SugarShop.Infrastructure.Persistence;
using SugarShop.Infrastructure.Persistence.Sales;

namespace SugarShop.Web.Controllers.Admin
{
    [Authorize(Roles = "Admin,Owner")]
    [Route("Admin/Gallery")]
    public class GalleryManagementController : Controller
    {
        private readonly SugarShopCatalogDbContext _context;
        private readonly SugarShopSalesDbContext _salesDb;
        private readonly IWebHostEnvironment _environment;

        public GalleryManagementController(
            SugarShopCatalogDbContext context,
            SugarShopSalesDbContext salesDb,
            IWebHostEnvironment environment)
        {
            _context = context;
            _salesDb = salesDb;
            _environment = environment;
        }

        #region Index
        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var items = await _context.GalleryItems
                .OrderBy(x => x.SortOrder)
                .ToListAsync();
            return View("~/Views/Admin/GalleryManagement/Index.cshtml", items);
        }
        #endregion

        #region HeaderSettings
        [HttpGet("HeaderSettings")]
        public async Task<IActionResult> HeaderSettings()
        {
            var settings = await _salesDb.GalleryHeaderSettings.FirstOrDefaultAsync()
                           ?? new GalleryHeaderSetting();
            return View("~/Views/Admin/GalleryManagement/HeaderSettings.cshtml", settings);
        }

        [HttpPost("HeaderSettings")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> HeaderSettings(GalleryHeaderSetting model)
        {
            try
            {
                var settings = await _salesDb.GalleryHeaderSettings.FirstOrDefaultAsync();
                bool isNew = (settings == null);

                if (isNew)
                {
                    settings = new GalleryHeaderSetting();
                    _salesDb.GalleryHeaderSettings.Add(settings);
                }

                // خواندن مسیر تصویر از Form (چون Media Picker آن را در input متنی قرار می‌دهد)
                var bgImagePath = Request.Form["BackgroundImagePath"].ToString();

                settings.Title = model.Title;
                settings.Subtitle = model.Subtitle;
                settings.BackgroundColor = model.BackgroundColor;
                settings.TextColor = model.TextColor;
                settings.Height = model.Height;
                settings.IsEnabled = model.IsEnabled;
                settings.BackgroundImagePath = string.IsNullOrWhiteSpace(bgImagePath) ? null : bgImagePath;
                settings.UpdatedAt = DateTime.UtcNow;

                await _salesDb.SaveChangesAsync();
                TempData["Success"] = "تنظیمات هدر گالری با موفقیت ذخیره شد.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"خطا: {ex.Message}";
            }

            return RedirectToAction(nameof(HeaderSettings));
        }
        #endregion

        #region Create
        [HttpGet("Create")]
        public IActionResult Create()
        {
            return View("~/Views/Admin/GalleryManagement/Create.cshtml");
        }

        [HttpPost("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(GalleryItem model)
        {
            if (!ModelState.IsValid)
            {
                return View("~/Views/Admin/GalleryManagement/Create.cshtml", model);
            }

            if (!string.IsNullOrWhiteSpace(model.FilePath))
            {
                string extension = Path.GetExtension(model.FilePath).ToLower();
                bool isVideo = extension == ".mp4" || extension == ".webm" || extension == ".mov";
                model.MediaType = isVideo ? "Video" : "Image";
            }

            if (string.IsNullOrWhiteSpace(model.ThumbnailPath) && model.MediaType == "Video")
            {
                model.ThumbnailPath = "/images/video-placeholder.jpg";
            }

            model.CreatedAt = DateTime.UtcNow;
            model.UpdatedAt = DateTime.UtcNow;

            _context.GalleryItems.Add(model);
            await _context.SaveChangesAsync();

            TempData["Success"] = "آیتم جدید با موفقیت ثبت شد.";
            return RedirectToAction(nameof(Index));
        }
        #endregion

        #region Edit
        [HttpGet("Edit/{id}")]
        public async Task<IActionResult> Edit(int id)
        {
            var item = await _context.GalleryItems.FindAsync(id);
            if (item == null)
                return NotFound();
            return View("~/Views/Admin/GalleryManagement/Edit.cshtml", item);
        }

        [HttpPost("Edit/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            GalleryItem model)
        {
            if (id != model.Id)
                return NotFound();

            var item = await _context.GalleryItems.FindAsync(id);
            if (item == null)
                return NotFound();

            if (!ModelState.IsValid)
            {
                return View("~/Views/Admin/GalleryManagement/Edit.cshtml", model);
            }

            item.Title = model.Title;
            item.Description = model.Description;
            item.Category = model.Category;
            item.SortOrder = model.SortOrder;
            item.IsActive = model.IsActive;
            item.UpdatedAt = DateTime.UtcNow;

            if (!string.IsNullOrWhiteSpace(model.FilePath))
            {
                item.FilePath = model.FilePath;
                string extension = Path.GetExtension(model.FilePath).ToLower();
                bool isVideo = extension == ".mp4" || extension == ".webm" || extension == ".mov";
                item.MediaType = isVideo ? "Video" : "Image";
            }

            if (!string.IsNullOrWhiteSpace(model.ThumbnailPath))
            {
                item.ThumbnailPath = model.ThumbnailPath;
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "اطلاعات با موفقیت ویرایش شد.";
            return RedirectToAction(nameof(Index));
        }
        #endregion

        #region Delete
        [HttpPost("Delete/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var item = await _context.GalleryItems.FindAsync(id);
            if (item == null)
                return RedirectToAction(nameof(Index));

            _context.GalleryItems.Remove(item);
            await _context.SaveChangesAsync();

            TempData["Success"] = "آیتم حذف شد.";
            return RedirectToAction(nameof(Index));
        }
        #endregion

        #region ToggleActive
        [HttpPost("ToggleActive/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(int id)
        {
            var item = await _context.GalleryItems.FindAsync(id);
            if (item == null)
                return RedirectToAction(nameof(Index));

            item.IsActive = !item.IsActive;
            item.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            TempData["Success"] = item.IsActive ? "آیتم فعال شد." : "آیتم غیرفعال شد.";
            return RedirectToAction(nameof(Index));
        }
        #endregion

    }
}