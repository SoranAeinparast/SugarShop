using System.Threading.Tasks;
using SugarShop.Domain.Entities;
using SugarShop.Infrastructure.Persistence.Sales;
using SugarShop.Web.Helpers;
using SugarShop.Web.Services.Interfaces;

namespace SugarShop.Web.Services.Implementations
{
    public class ContentStorageService : IContentStorageService
    {
        private readonly SugarShopSalesDbContext _context;

        public ContentStorageService(SugarShopSalesDbContext context)
        {
            _context = context;
        }

        public async Task<int> SaveContentAsync(EducationalContent content)
        {
            // پاک‌سازی HTML برای جلوگیری از XSS (محتوا از منابع خارجی/هوش مصنوعی می‌آید)
            content.BodyHtml = HtmlSanitizerHelper.Sanitize(content.BodyHtml);

            content.CreatedAt = System.DateTime.UtcNow;

            // ✅ انتشار خودکار ممنوع است؛ محتوا ابتدا باید توسط ادمین تأیید و منتشر شود
            content.IsPublished = false;
            content.PublishedAt = null;
            content.IsApproved = false;
            content.ApprovedAt = null;

            _context.EducationalContents.Add(content);
            await _context.SaveChangesAsync();
            return content.Id;
        }
    }
}