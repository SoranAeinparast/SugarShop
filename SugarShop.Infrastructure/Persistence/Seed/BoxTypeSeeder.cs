using Microsoft.EntityFrameworkCore;
using SugarShop.Domain.Entities;

namespace SugarShop.Infrastructure.Persistence.Seed
{
    public class BoxTypeSeeder
    {
        public static async Task SeedAsync(SugarShopCatalogDbContext db)
        {
            if (await db.BoxTypes.AnyAsync())
                return;

            var boxTypes = new List<BoxType>
            {
                new BoxType { TitleFa = "نیم کیلویی", CapacityGrams = 500, MaxRows = 2, SortOrder = 1, IsActive = true },
                new BoxType { TitleFa = "یک کیلویی", CapacityGrams = 1000, MaxRows = 3, SortOrder = 2, IsActive = true },
                new BoxType { TitleFa = "دو کیلویی", CapacityGrams = 2000, MaxRows = 5, SortOrder = 3, IsActive = true }
            };

            db.BoxTypes.AddRange(boxTypes);
            await db.SaveChangesAsync();
        }
    }
}