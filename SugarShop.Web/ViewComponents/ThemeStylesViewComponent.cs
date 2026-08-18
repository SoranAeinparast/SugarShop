using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SugarShop.Domain.Entities;
using SugarShop.Infrastructure.Persistence.Sales;

namespace SugarShop.Web.ViewComponents
{
    public class ThemeStylesViewComponent : ViewComponent
    {
        private readonly SugarShopSalesDbContext _context;

        public ThemeStylesViewComponent(SugarShopSalesDbContext context)
        {
            _context = context;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var setting = await _context.ThemeSettings
                .OrderByDescending(t => t.Id)
                .FirstOrDefaultAsync() ?? new ThemeSetting();

            return View(setting);
        }
    }
}