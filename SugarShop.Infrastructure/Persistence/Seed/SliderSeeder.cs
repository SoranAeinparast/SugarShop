using Microsoft.EntityFrameworkCore;
using SugarShop.Domain.Entities;

namespace SugarShop.Infrastructure.Persistence.Seed
{
    public class SliderSeeder
    {
        public static async Task SeedAsync(SugarShopCatalogDbContext db)
        {
            if (await db.Sliders.AnyAsync())
                return;

            var sliders = new List<Slider>
            {
                new Slider
                {
                    Title = "تخفیف ویژه شیرینی‌های نوروزی",
                    Subtitle = "سفارش آنلاین آسان و سریع",
                    ButtonText = "مشاهده دسته‌بندی‌ها",
                    ButtonUrl = "/",
                    ImagePath = "/images/sliders/slider1.jpg",
                    SortOrder = 1,
                    IsActive = true,
                    StartAt = DateTime.UtcNow.AddDays(-1),
                    EndAt = DateTime.UtcNow.AddDays(30)
                },
                new Slider
                {
                    Title = "کیک‌های خاص تولد",
                    Subtitle = "طعم تازه، طراحی جذاب",
                    ButtonText = "دیدن کیک‌ها",
                    ButtonUrl = "/products/cakes",
                    ImagePath = "/images/sliders/slider2.jpg",
                    SortOrder = 2,
                    IsActive = true,
                    StartAt = DateTime.UtcNow.AddDays(-1),
                    EndAt = DateTime.UtcNow.AddDays(30)
                }
            };

            db.Sliders.AddRange(sliders);
            await db.SaveChangesAsync();
        }
    }
}