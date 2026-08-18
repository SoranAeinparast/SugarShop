using Microsoft.EntityFrameworkCore;
using SugarShop.Domain.Entities;

namespace SugarShop.Infrastructure.Persistence
{
    public class SugarShopCatalogDbContext : DbContext
    {
        public SugarShopCatalogDbContext(DbContextOptions<SugarShopCatalogDbContext> options)
            : base(options)
        {
        }

        public DbSet<Category> Categories => Set<Category>();
        public DbSet<Product> Products => Set<Product>();
        public DbSet<Slider> Sliders => Set<Slider>();
        public DbSet<BoxType> BoxTypes => Set<BoxType>();
        public DbSet<SweetItem> SweetItems => Set<SweetItem>();
        public DbSet<GalleryItem> GalleryItems => Set<GalleryItem>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Category>(e =>
            {
                e.HasIndex(x => x.Slug).IsUnique(false);
            });

            modelBuilder.Entity<Product>(e =>
            {
                e.HasIndex(x => x.Slug).IsUnique(false);

                e.Property(p => p.Price)
                    .HasPrecision(18, 2);

                e.Property(p => p.WeightGrams)
                    .HasPrecision(18, 0);

                e.HasOne(x => x.Category)
                 .WithMany()
                 .HasForeignKey(x => x.CategoryId);
            });

            modelBuilder.Entity<Slider>(e =>
            {
                // فعلاً لازم نیست
            });

            // ========== اصلاح شده برای SweetItem با فیلدهای جدید ==========
            modelBuilder.Entity<SweetItem>(e =>
            {
                e.HasIndex(x => x.Slug).IsUnique(false);

                // فیلدهای جدید
                e.Property(p => p.ApproxWeightGrams)
                    .HasPrecision(18, 0);  // عدد صحیح

                e.Property(p => p.PricePerKg)
                    .HasPrecision(18, 2);  // دو رقم اعشار

                // فیلدهای text نیازی به تنظیم خاصی ندارند
                // ImagePath و Description

                e.HasOne(x => x.Category)
                 .WithMany()
                 .HasForeignKey(x => x.CategoryId);
            });

            modelBuilder.Entity<BoxType>(e =>
            {
                e.HasIndex(x => x.TitleFa).IsUnique(false);
            });

            // ========== تنظیمات گالری ===================
            modelBuilder.Entity<GalleryItem>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Title).HasMaxLength(200);
                entity.Property(e => e.Description).HasMaxLength(1000);
                entity.Property(e => e.MediaType).HasMaxLength(10);
                entity.Property(e => e.FilePath).HasMaxLength(500);
                entity.Property(e => e.ThumbnailPath).HasMaxLength(500);
                entity.Property(e => e.Category).HasMaxLength(100);
                entity.HasIndex(e => e.IsActive);
                entity.HasIndex(e => e.SortOrder);
            });
        }
    }
}