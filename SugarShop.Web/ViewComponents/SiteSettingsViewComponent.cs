using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SugarShop.Infrastructure.Persistence.Sales;

namespace SugarShop.Web.ViewComponents
{
    public class SiteSettingsViewComponent : ViewComponent
    {
        private readonly SugarShopSalesDbContext _context;

        public SiteSettingsViewComponent(SugarShopSalesDbContext context)
        {
            _context = context;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var settings = await _context.SiteSettings.FirstOrDefaultAsync();
            return View(settings ?? new Domain.Entities.SiteSetting());
        }
    }
}