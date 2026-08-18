using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SugarShop.Domain.Entities;
using SugarShop.Infrastructure.Persistence.Sales;
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
            var content = await _context.EducationalContents.FindAsync(id);
            if (content == null) return NotFound();
            return View(content);
        }
    }
}