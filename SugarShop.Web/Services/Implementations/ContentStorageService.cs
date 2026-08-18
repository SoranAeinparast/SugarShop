using System.Threading.Tasks;
using SugarShop.Domain.Entities;
using SugarShop.Infrastructure.Persistence.Sales;
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
            content.CreatedAt = System.DateTime.UtcNow;
            content.PublishedAt = System.DateTime.UtcNow;
            content.IsPublished = true;
            _context.EducationalContents.Add(content);
            await _context.SaveChangesAsync();
            return content.Id;
        }
    }
}