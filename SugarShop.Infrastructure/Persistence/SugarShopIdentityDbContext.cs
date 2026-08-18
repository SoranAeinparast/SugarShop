using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SugarShop.Domain.Entities;

namespace SugarShop.Infrastructure.Persistence
{
    public class SugarShopIdentityDbContext : IdentityDbContext<ApplicationUser> // تغییر از IdentityUser به ApplicationUser
    {
        public SugarShopIdentityDbContext(DbContextOptions<SugarShopIdentityDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            // می‌توانید تنظیمات اضافی مانند max length برای FullName را در اینجا اضافه کنید
            builder.Entity<ApplicationUser>(e =>
            {
                e.Property(u => u.FullName).HasMaxLength(100);
                e.Property(u => u.AvatarPath).HasMaxLength(500);
            });
        }
    }
}