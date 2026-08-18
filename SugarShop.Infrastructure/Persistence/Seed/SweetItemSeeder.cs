using Microsoft.EntityFrameworkCore;
using SugarShop.Domain.Entities;

namespace SugarShop.Infrastructure.Persistence.Seed
{
    public class SweetItemSeeder
    {
        public static async Task SeedAsync(SugarShopCatalogDbContext db)
        {
            if (await db.SweetItems.AnyAsync())
                return;
            async Task<int> GetCategoryIdBySlug(string slug)
            {
                var cat = await db.Categories.FirstOrDefaultAsync(c => c.Slug == slug);
                if (cat == null) throw new InvalidOperationException($"Category with slug '{slug}' not found.");
                return cat.Id;
            }

            var dryId = await GetCategoryIdBySlug("sugar-dry");
            var wetId = await GetCategoryIdBySlug("sugar-wet");
            var homeId = await GetCategoryIdBySlug("home-sugar");

            var items = new List<SweetItem>
            {
                new SweetItem
                {
                    TitleFa = "زبان",
                    Slug = "zaban",
                    CategoryId = dryId,
                    ApproxWeightGrams = 50,      // وزن تقریبی هر عدد
                    PricePerKg = 900000,         // قیمت هر کیلوگرم (900,000 تومان)
                    InventoryCount = 50,
                    SortOrder = 1,
                    IsActive = true,
                    Description = "شیرینی سنتی و خوشمزه زبان با طعم لطیف",
                    ImagePath = "/images/sweets/zaban.jpg"
                },
                new SweetItem
                {
                    TitleFa = "دانمارکی",
                    Slug = "danmarki",
                    CategoryId = wetId,
                    ApproxWeightGrams = 70,
                    PricePerKg = 1100000,        // قیمت هر کیلوگرم (1,100,000 تومان)
                    InventoryCount = 50,
                    SortOrder = 2,
                    IsActive = true,
                    Description = "شیرینی دانمارکی با طعم کره و لایه‌های ترد",
                    ImagePath = "/images/sweets/danmarki.jpg"
                },
                new SweetItem
                {
                    TitleFa = "نخودچی",
                    Slug = "nokhodchi",
                    CategoryId = homeId,
                    ApproxWeightGrams = 40,
                    PricePerKg = 800000,         // قیمت هر کیلوگرم (800,000 تومان)
                    InventoryCount = 50,
                    SortOrder = 3,
                    IsActive = true,
                    Description = "شیرینی سنتی نخودچی با طعم هل و زعفران",
                    ImagePath = "/images/sweets/nokhodchi.jpg"
                },

                // چند مورد بیشتر برای تست
                new SweetItem
                {
                    TitleFa = "شیرینی خشک گردویی",
                    Slug = "walnut-dry",
                    CategoryId = dryId,
                    ApproxWeightGrams = 60,
                    PricePerKg = 950000,
                    InventoryCount = 30,
                    SortOrder = 4,
                    IsActive = true,
                    Description = "شیرینی خشک با طعم گردو و هل",
                    ImagePath = "/images/sweets/walnut-dry.jpg"
                },
                new SweetItem
                {
                    TitleFa = "شیرینی تر کشمش",
                    Slug = "raisin-wet",
                    CategoryId = wetId,
                    ApproxWeightGrams = 65,
                    PricePerKg = 1050000,
                    InventoryCount = 25,
                    SortOrder = 5,
                    IsActive = true,
                    Description = "شیرینی تر با کشمش و طعم دلچسب",
                    ImagePath = "/images/sweets/raisin-wet.jpg"
                }
            };

            db.SweetItems.AddRange(items);
            await db.SaveChangesAsync();
        }
    }
}