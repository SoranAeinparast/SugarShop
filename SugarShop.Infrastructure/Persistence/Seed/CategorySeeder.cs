using Microsoft.EntityFrameworkCore;
using SugarShop.Domain.Entities;
using SugarShop.Infrastructure.Persistence;

namespace SugarShop.Infrastructure.Persistence.Seed
{
    public class CategorySeeder
    {
        public static async Task SeedAsync(SugarShopCatalogDbContext db)
        {
            // اگر قبلاً داده هست، چیزی اضافه نکن
            if (await db.Categories.AnyAsync())
                return;

            var categories = new List<Category>
            {
                new Category { TitleFa = "شیرینی خشک", Slug = "sugar-dry", SortOrder = 1, RequiresBoxSelection = true },
                new Category { TitleFa = "شیرینی تر", Slug = "sugar-wet", SortOrder = 2, RequiresBoxSelection = true },
                new Category { TitleFa = "شیرینی خانگی", Slug = "home-sugar", SortOrder = 3, RequiresBoxSelection = true },
                new Category { TitleFa = "انواع کیک", Slug = "cakes", SortOrder = 4, RequiresBoxSelection = false },
                new Category { TitleFa = "دسر", Slug = "desserts", SortOrder = 5, RequiresBoxSelection = false },
                new Category { TitleFa = "شکلات", Slug = "chocolates", SortOrder = 6, RequiresBoxSelection = false },
                new Category { TitleFa = "بستنی", Slug = "ice-cream", SortOrder = 7, RequiresBoxSelection = false },
                new Category { TitleFa = "لوازم تولد", Slug = "birthday-items", SortOrder = 8, RequiresBoxSelection = false },
            };

            db.Categories.AddRange(categories);
            await db.SaveChangesAsync();
        }
    }
}