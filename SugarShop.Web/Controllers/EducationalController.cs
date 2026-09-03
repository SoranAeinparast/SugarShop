using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SugarShop.Domain.Entities;
using SugarShop.Infrastructure.Persistence.Sales;
using SugarShop.Web.Helpers;
using System.Linq;
using System.Threading.Tasks;

namespace SugarShop.Web.Controllers
{
        public class EducationalController : Controller
    {
        private readonly SugarShopSalesDbContext _context;

        public EducationalController(SugarShopSalesDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var contents = await _context.EducationalContents
                .Where(c => c.IsPublished)
                .OrderByDescending(c => c.PublishedAt)
                .Take(20)
                .ToListAsync();
            return View(contents);
        }

        public async Task<IActionResult> Details(int id)
        {
            // ✅ فقط محتوای منتشرشده برای عموم قابل مشاهده است (پیش‌نویس/تأییدنشده دیده نمی‌شود)
            var content = await _context.EducationalContents
                .Where(c => c.Id == id && c.IsPublished)
                .FirstOrDefaultAsync();
            if (content == null) return NotFound();

            // پاک‌سازی سمت خروج برای محتوای قدیمی‌ای که قبلاً پاک‌سازی نشده است
            content.BodyHtml = HtmlSanitizerHelper.Sanitize(content.BodyHtml);
            return View(content);
        }
    }
}