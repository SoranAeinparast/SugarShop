using Microsoft.EntityFrameworkCore;
using SugarShop.Domain.Entities;
using SugarShop.Infrastructure.Persistence.Sales;

namespace SugarShop.Infrastructure.Persistence.Seed
{
    public class SplashSettingSeeder
    {
        public static async Task SeedAsync(SugarShopSalesDbContext db)
        {
            if (!await db.SplashSettings.AnyAsync())
            {
                db.SplashSettings.Add(new SplashSetting
                {
                    VideoPath = "/videos/garmsar.mp4",
                    ButtonText = "ورود به سایت",   // ← تغییر از ButtonTitle
                    IsEnabled = true,              // ← تغییر از IsActive
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
                await db.SaveChangesAsync();
            }
        }
    }
}