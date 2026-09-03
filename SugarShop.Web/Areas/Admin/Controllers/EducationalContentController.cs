using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SugarShop.Domain.Entities;
using SugarShop.Infrastructure.Persistence.Sales;
using SugarShop.Web.Helpers;
using SugarShop.Web.Services.Interfaces;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SugarShop.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,Owner")]
    [Route("Admin/[controller]")]
    public class EducationalContentController : Controller
    {
        private readonly SugarShopSalesDbContext _context;
        private readonly IAIContentService _aiContentService;
        private readonly ILogger<EducationalContentController> _logger;

        public EducationalContentController(SugarShopSalesDbContext context, IAIContentService aiContentService, ILogger<EducationalContentController> logger)
        {
            _context = context;
            _aiContentService = aiContentService;
            _logger = logger;
        }
        [HttpGet]
        public async Task<IActionResult> Index(
            string? search = null,
            string? category = null,
            bool? isPublished = null,
            bool? isApproved = null,
            string? sortBy = "createdAt_desc",
            int page = 1,
            int pageSize = 20)
        {
            var query = _context.EducationalContents
                .Include(c => c.Source)
                .Include(c => c.Topic)
                .AsQueryable();
            if (!string.IsNullOrEmpty(search))
            {
                search = search.Trim();
                query = query.Where(c =>
                    c.Title.Contains(search) ||
                    c.BodyHtml.Contains(search) ||
                    c.Tags.Contains(search) ||
                    (c.AdminNotes != null && c.AdminNotes.Contains(search)));
            }
            if (!string.IsNullOrEmpty(category))
            {
                query = query.Where(c => c.Category == category);
            }
            if (isPublished.HasValue)
            {
                query = query.Where(c => c.IsPublished == isPublished.Value);
            }
            if (isApproved.HasValue)
            {
                query = query.Where(c => c.IsApproved == isApproved.Value);
            }
            query = sortBy switch
            {
                "title_asc" => query.OrderBy(c => c.Title),
                "title_desc" => query.OrderByDescending(c => c.Title),
                "createdAt_asc" => query.OrderBy(c => c.CreatedAt),
                "createdAt_desc" => query.OrderByDescending(c => c.CreatedAt),
                "publishedAt_asc" => query.OrderBy(c => c.PublishedAt),
                "publishedAt_desc" => query.OrderByDescending(c => c.PublishedAt),
                _ => query.OrderByDescending(c => c.CreatedAt)
            };
            var categories = await _context.EducationalContents
                .Where(c => !string.IsNullOrEmpty(c.Category))
                .Select(c => c.Category!)
                .Distinct()
                .ToListAsync();
            var totalCount = await query.CountAsync();
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Search = search;
            ViewBag.Category = category;
            ViewBag.IsPublished = isPublished;
            ViewBag.IsApproved = isApproved;
            ViewBag.SortBy = sortBy;
            ViewBag.Categories = categories;
            ViewBag.CurrentPage = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalCount = totalCount;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);

            return View(items);
        }
        [HttpGet("Details/{id}")]
        public async Task<IActionResult> Details(int id)
        {
            var content = await _context.EducationalContents
                .Include(c => c.Source)
                .Include(c => c.Topic)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (content == null) return NotFound();

            return View(content);
        }
        [HttpGet("Edit/{id}")]
        public async Task<IActionResult> Edit(int id)
        {
            var content = await _context.EducationalContents
                .Include(c => c.Source)
                .Include(c => c.Topic)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (content == null) return NotFound();
            ViewBag.Sources = await _context.ContentSources
                .Where(s => s.IsActive)
                .ToListAsync();
            ViewBag.Topics = await _context.ContentTopics
                .Where(t => t.IsActive)
                .ToListAsync();

            return View(content);
        }
        [HttpPost("Edit/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, EducationalContent model)
        {
            if (id != model.Id) return NotFound();

            if (!ModelState.IsValid)
            {
                ViewBag.Sources = await _context.ContentSources
                    .Where(s => s.IsActive)
                    .ToListAsync();
                ViewBag.Topics = await _context.ContentTopics
                    .Where(t => t.IsActive)
                    .ToListAsync();
                return View(model);
            }
            var content = await _context.EducationalContents.FindAsync(id);
            if (content == null) return NotFound();

            content.Title = model.Title;
            content.BodyHtml = HtmlSanitizerHelper.Sanitize(model.BodyHtml);
            content.FeaturedImageUrl = model.FeaturedImageUrl;
            content.Category = model.Category;
            content.Tags = model.Tags;
            content.SourceId = model.SourceId;
            content.TopicId = model.TopicId;
            content.AdminNotes = model.AdminNotes;
            content.MetaDescription = model.MetaDescription;
            content.IsPublished = model.IsPublished;
            if (model.IsPublished && !content.IsPublished)
            {
                content.PublishedAt = DateTime.UtcNow;
            }
            else if (!model.IsPublished)
            {
                content.PublishedAt = null;
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "✅ محتوا با موفقیت ویرایش شد.";
            return RedirectToAction(nameof(Index));
        }
        [HttpPost("Approve/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            var content = await _context.EducationalContents.FindAsync(id);
            if (content == null)
            {
                TempData["Error"] = "❌ محتوا یافت نشد.";
                return RedirectToAction(nameof(Index));
            }
            if (!content.IsApproved)
            {
                content.IsApproved = true;
                content.ApprovedAt = DateTime.UtcNow;
                var settings = await _context.AIContentSettings.FirstOrDefaultAsync();
                if (settings != null && settings.AutoPublishEnabled)
                {
                    content.IsPublished = true;
                    content.PublishedAt = DateTime.UtcNow;
                    TempData["Success"] = "✅ محتوا تایید و به‌صورت خودکار منتشر شد.";
                }
                else
                {
                    TempData["Success"] = "✅ محتوای آموزشی با موفقیت تایید شد. برای انتشار آن را منتشر کنید.";
                }

                await _context.SaveChangesAsync();
            }
            else
            {
                TempData["Warning"] = "⚠️ این محتوا قبلاً تایید شده است.";
            }

            return RedirectToAction(nameof(Index));
        }
        [HttpPost("Unapprove/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Unapprove(int id)
        {
            var content = await _context.EducationalContents.FindAsync(id);
            if (content == null)
            {
                TempData["Error"] = "❌ محتوا یافت نشد.";
                return RedirectToAction(nameof(Index));
            }

            if (content.IsApproved)
            {
                content.IsApproved = false;
                content.ApprovedAt = null;
                await _context.SaveChangesAsync();
                TempData["Success"] = "✅ تایید محتوا لغو شد.";
            }
            else
            {
                TempData["Warning"] = "⚠️ این محتوا قبلاً تایید نشده است.";
            }

            return RedirectToAction(nameof(Index));
        }
        [HttpPost("TogglePublish/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TogglePublish(int id)
        {
            var content = await _context.EducationalContents.FindAsync(id);
            if (content == null)
            {
                TempData["Error"] = "❌ محتوا یافت نشد.";
                return RedirectToAction(nameof(Index));
            }
            if (!content.IsApproved && !content.IsPublished)
            {
                TempData["Error"] = "❌ این محتوا هنوز تایید نشده است. ابتدا آن را تایید کنید.";
                return RedirectToAction(nameof(Index));
            }

            content.IsPublished = !content.IsPublished;
            content.PublishedAt = content.IsPublished ? DateTime.UtcNow : null;

            await _context.SaveChangesAsync();
            TempData["Success"] = content.IsPublished
                ? "✅ محتوا با موفقیت منتشر شد."
                : "✅ انتشار محتوا لغو شد.";

            return RedirectToAction(nameof(Index));
        }
        [HttpPost("Delete/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var content = await _context.EducationalContents.FindAsync(id);
            if (content != null)
            {
                _context.EducationalContents.Remove(content);
                await _context.SaveChangesAsync();
                TempData["Success"] = "✅ محتوا با موفقیت حذف شد.";
            }
            else
            {
                TempData["Error"] = "❌ محتوا یافت نشد.";
            }

            return RedirectToAction(nameof(Index));
        }
        [HttpGet("Create")]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(EducationalContent model)
        {
            if (!ModelState.IsValid)
                return View(model);

            model.CreatedAt = DateTime.UtcNow;
            model.IsApproved = false;
            model.IsPublished = false;
            model.BodyHtml = HtmlSanitizerHelper.Sanitize(model.BodyHtml);

            _context.EducationalContents.Add(model);
            await _context.SaveChangesAsync();

            TempData["Success"] = "محتوای آموزشی با موفقیت ایجاد شد.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost("GenerateAI")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateAI(string title, string content)
        {
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(content))
                return Json(new { success = false, message = "عنوان و متن الزامی است." });

            try
            {
                var rewritten = await _aiContentService.RewriteContentAsync(title, content);
                var featuredImage = await _aiContentService.GenerateFeaturedImageAsync(title,
                    content.Length > 100 ? content.Substring(0, 100) : content);

                var newContent = new EducationalContent
                {
                    Title = title,
                    BodyHtml = HtmlSanitizerHelper.Sanitize(rewritten),
                    FeaturedImageUrl = featuredImage,
                    Category = "آموزشی",
                    Tags = "شیرینی, کیک, آموزش",
                    IsPublished = false,
                    IsApproved = false,
                    CreatedAt = DateTime.UtcNow
                };

                _context.EducationalContents.Add(newContent);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "محتوا با AI تولید و ذخیره شد." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating AI content");
                return Json(new { success = false, message = $"خطا در تولید محتوا: {ex.Message}" });
            }
        }

        [HttpGet("GetContentList")]
        public async Task<IActionResult> GetContentList(
            string? search = null,
            bool? isPublished = null,
            int page = 1,
            int pageSize = 20)
        {
            var query = _context.EducationalContents
                .Where(c => !string.IsNullOrEmpty(c.Title))
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(c => c.Title.Contains(search));
            }

            if (isPublished.HasValue)
            {
                query = query.Where(c => c.IsPublished == isPublished.Value);
            }

            var totalCount = await query.CountAsync();
            var items = await query
                .OrderByDescending(c => c.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new
                {
                    c.Id,
                    c.Title,
                    c.Category,
                    c.IsPublished,
                    c.IsApproved,
                    c.CreatedAt,
                    c.PublishedAt,
                    HasImage = !string.IsNullOrEmpty(c.FeaturedImageUrl)
                })
                .ToListAsync();

            return Ok(new
            {
                data = items,
                total = totalCount,
                page,
                pageSize
            });
        }
    }
}