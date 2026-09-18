using Microsoft.EntityFrameworkCore;
using SugarShop.Domain.Entities;
using SugarShop.Domain.Entities.Newsletter;
using SugarShop.Domain.Entities.Sales;
using SugarShop.Domain.Entities.Sms;

namespace SugarShop.Infrastructure.Persistence.Sales
{
    public class SugarShopSalesDbContext : DbContext
    {
        public SugarShopSalesDbContext(DbContextOptions<SugarShopSalesDbContext> options) : base(options) { }
        public DbSet<Address> Addresses => Set<Address>();
        public DbSet<Order> Orders => Set<Order>();
        public DbSet<OrderItem> OrderItems => Set<OrderItem>();
        public DbSet<Payment> Payments => Set<Payment>();

        /// <summary>توکن‌های موقت دیدن صورت‌حساب پرداخت (لینک پیامکی بدون نیاز به ورود).</summary>
        public DbSet<StatementLink> StatementLinks => Set<StatementLink>();

        /// <summary>اثرسنجی پیامک‌های لینک‌دار: چه زمانی رفت و چند بار باز شد.</summary>
        public DbSet<SmsLinkTracking> SmsLinkTrackings => Set<SmsLinkTracking>();
        public DbSet<Wallet> Wallets => Set<Wallet>();
        public DbSet<WalletTransaction> WalletTransactions => Set<WalletTransaction>();
        public DbSet<DiscountCode> DiscountCodes => Set<DiscountCode>();
        public DbSet<Ticket> Tickets => Set<Ticket>();
        public DbSet<PaymentGateway> PaymentGateways { get; set; }
        public DbSet<PaymentGatewayAccount> PaymentGatewayAccounts { get; set; }
        public DbSet<Subscriber> Subscribers => Set<Subscriber>();
        public DbSet<NewsletterSetting> NewsletterSettings => Set<NewsletterSetting>();
        public DbSet<SiteSetting> SiteSettings => Set<SiteSetting>();
        public DbSet<Slider> Sliders { get; set; }
        public DbSet<BoxFinalInfo> BoxFinalInfos => Set<BoxFinalInfo>();
        public DbSet<ThemeSetting> ThemeSettings => Set<ThemeSetting>();
        public DbSet<MenuItem> MenuItems => Set<MenuItem>();
        public DbSet<WalletSettings> WalletSettings { get; set; }
        public DbSet<BirthdayReminder> BirthdayReminders => Set<BirthdayReminder>();
        public DbSet<CustomCakeOrder> CustomCakeOrders { get; set; }
        public DbSet<ContactMessage> ContactMessages { get; set; }
        public DbSet<EducationalContent> EducationalContents => Set<EducationalContent>();
        public DbSet<AppDownloadLog> AppDownloadLogs { get; set; }
        public DbSet<AIContentSettings> AIContentSettings => Set<AIContentSettings>();
        public DbSet<ContentSource> ContentSources => Set<ContentSource>();
        public DbSet<ContentTopic> ContentTopics => Set<ContentTopic>();
        public DbSet<SplashSetting> SplashSettings { get; set; }
        public DbSet<AboutUsSetting> AboutUsSettings { get; set; }
        public DbSet<MediaAsset> MediaAssets { get; set; }
        public DbSet<GalleryHeaderSetting> GalleryHeaderSettings { get; set; }
        public DbSet<CategoryHeaderSetting> CategoryHeaderSettings { get; set; }
        public DbSet<EducationalHeaderSetting> EducationalHeaderSettings { get; set; }
        public DbSet<Invoice> Invoices { get; set; }
        public DbSet<InvoiceItem> InvoiceItems { get; set; }

        // ── سامانه پیامکی ──
        public DbSet<SmsLog> SmsLogs { get; set; }
        public DbSet<SmsTemplate> SmsTemplates { get; set; }
        public DbSet<SmsSystemSetting> SmsSystemSettings { get; set; }
        public DbSet<SmsOtpCode> SmsOtpCodes { get; set; }
        public DbSet<RestockSubscription> RestockSubscriptions { get; set; }
        public DbSet<SmsAutoReminderLog> SmsAutoReminderLogs { get; set; }
        public DbSet<SmsCampaign> SmsCampaigns { get; set; }
        public DbSet<SmsCampaignRecipient> SmsCampaignRecipients { get; set; }
        public DbSet<SmsOutboxItem> SmsOutboxItems { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<WalletSettings>(entity =>
            {
                entity.ToTable("WalletSettings");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.IsEnabled).HasDefaultValue(true);
                entity.Property(e => e.ReturnType).HasMaxLength(20).HasDefaultValue("Percentage");
                entity.Property(e => e.ReturnValue).HasColumnType("decimal(18,2)").HasDefaultValue(0);
                entity.Property(e => e.MinimumOrderAmount).HasColumnType("decimal(18,2)").HasDefaultValue(0);
                entity.Property(e => e.AllowDirectRecharge).HasDefaultValue(true);
                entity.Property(e => e.DirectRechargeMinAmount).HasColumnType("decimal(18,2)").HasDefaultValue(10000);
                entity.Property(e => e.DirectRechargeMaxAmount).HasColumnType("decimal(18,2)").HasDefaultValue(0);
                entity.Property(e => e.MaxWalletBalance).HasColumnType("decimal(18,2)").HasDefaultValue(0);
                entity.Property(e => e.ExpiryDays).HasDefaultValue(0);
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("GETUTCDATE()");
            });
            modelBuilder.Entity<Order>(e =>
            {
                e.HasKey(x => x.Id);
                e.HasIndex(x => x.UserId);
                e.Property(x => x.OrderCode).HasMaxLength(50);
                e.HasIndex(x => x.OrderCode).IsUnique();
                e.Property(x => x.TotalAmountSnapshot).HasPrecision(18, 2);
                e.Property(x => x.DiscountAmountSnapshot).HasPrecision(18, 2);
                e.Property(x => x.DeliveryFeeSnapshot).HasPrecision(18, 2);
                e.Property(x => x.TaxAmountSnapshot).HasPrecision(18, 2);
                e.Property(x => x.FinalTotalAmount).HasPrecision(18, 2);
                e.Property(x => x.DiscountAmount).HasPrecision(18, 2);
                e.HasOne(x => x.CustomerAddress)
                    .WithMany()
                    .HasForeignKey(x => x.CustomerAddressId)
                    .OnDelete(DeleteBehavior.Restrict);
                e.HasIndex(x => new { x.UserId, x.CreatedAt });
                // ایندکس‌های لیست سفارش‌های پنل ادمین: مرتب‌سازی زمانی و فیلتر وضعیت هر دو سمت سرور
                // انجام می‌شوند، پس بدون این دو ایندکس با زیاد شدن سفارش‌ها هر صفحه اسکن کامل می‌خواست.
                e.HasIndex(x => x.CreatedAt);
                e.HasIndex(x => new { x.OrderStatus, x.CreatedAt });
                e.Property(x => x.CreatedAt).HasColumnType("datetime2");
                e.Property(x => x.UpdatedAt).HasColumnType("datetime2");
                e.Property(x => x.InventoryDeductedAt).HasColumnType("datetime2");
                e.HasOne(x => x.DiscountCode)
                    .WithMany()
                    .HasForeignKey(x => x.DiscountCodeId)
                    .OnDelete(DeleteBehavior.SetNull);
            });
            modelBuilder.Entity<Wallet>(e =>
            {
                e.HasKey(x => x.Id);
                e.HasIndex(x => x.UserId);
                e.Property(x => x.Balance).HasPrecision(18, 2);
            });
            modelBuilder.Entity<WalletTransaction>(e =>
            {
                e.HasKey(x => x.Id);
                e.HasIndex(x => x.UserId);
                e.HasIndex(x => x.OrderId);
                e.Property(x => x.Amount).HasPrecision(18, 2);
            });
            modelBuilder.Entity<OrderItem>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.UnitPriceSnapshot).HasPrecision(18, 2);
                e.Property(x => x.TotalPriceSnapshot).HasPrecision(18, 2);
                e.HasIndex(x => x.OrderId);
            });
            modelBuilder.Entity<SmsLinkTracking>(e =>
            {
                e.HasKey(x => x.Id);
                // برای هر سفارش، هر نوع لینک فقط یک ردیف دارد (ارسال و بازدیدها روی همان به‌روز می‌شوند)
                e.HasIndex(x => new { x.OrderId, x.Kind }).IsUnique();
                e.HasIndex(x => x.Kind);
                e.Property(x => x.SentAt).HasColumnType("datetime2");
                e.Property(x => x.FirstOpenedAt).HasColumnType("datetime2");
                e.Property(x => x.LastOpenedAt).HasColumnType("datetime2");
            });

            modelBuilder.Entity<StatementLink>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.Token).HasMaxLength(64).IsRequired();
                // توکن در آدرس می‌آید، پس جست‌وجو باید ایندکس‌دار و یکتا باشد
                e.HasIndex(x => x.Token).IsUnique();
                e.HasIndex(x => x.OrderId);
                e.Property(x => x.CreatedAt).HasColumnType("datetime2");
                e.Property(x => x.ExpiresAt).HasColumnType("datetime2");
                e.Property(x => x.LastOpenedAt).HasColumnType("datetime2");
            });
            modelBuilder.Entity<Payment>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.Amount).HasPrecision(18, 2);
                e.HasIndex(x => x.Authority);
                e.HasIndex(x => x.OrderId);
                e.Property(x => x.CreatedAt).HasColumnType("datetime2");
            });
            modelBuilder.Entity<DiscountCode>(e =>
            {
                e.HasKey(x => x.Id);
                e.HasIndex(x => x.Code).IsUnique();
                e.Property(x => x.DiscountValue).HasPrecision(18, 2);
                e.Property(x => x.MinimumOrderAmount).HasPrecision(18, 2);
                e.Property(x => x.UsedCount).HasDefaultValue(0);
                e.Property(x => x.IsActive).HasDefaultValue(true);
                e.Property(x => x.CreatedAt).HasColumnType("datetime2");
                e.Property(x => x.UpdatedAt).HasColumnType("datetime2");
            });
            modelBuilder.Entity<Ticket>(e =>
            {
                e.HasKey(x => x.Id);
                e.HasIndex(x => x.UserId);
                e.Property(x => x.CreatedAt).HasColumnType("datetime2");
            });
            modelBuilder.Entity<PaymentGateway>(entity =>
            {
                entity.ToTable("PaymentGateways");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.GatewayType).IsUnique();
                entity.Property(e => e.Name).HasMaxLength(50).IsRequired();
                entity.Property(e => e.Title).HasMaxLength(100).IsRequired();
                entity.Property(e => e.GatewayType).HasMaxLength(50).IsRequired();
            });
            modelBuilder.Entity<PaymentGatewayAccount>(entity =>
            {
                entity.ToTable("PaymentGatewayAccounts");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.GatewayId, e.Title });
                entity.HasIndex(e => e.GatewayId); 
                entity.Property(e => e.Title).HasMaxLength(100).IsRequired();
                entity.Property(e => e.ConfigData).HasColumnType("nvarchar(max)");
                entity.HasOne(e => e.Gateway)
                      .WithMany(g => g.Accounts)
                      .HasForeignKey(e => e.GatewayId)
                      .OnDelete(DeleteBehavior.Cascade);
            });
            modelBuilder.Entity<Subscriber>(e =>
            {
                e.HasKey(x => x.Id);
                e.HasIndex(x => x.Email).IsUnique();
                e.Property(x => x.CreatedAt).HasColumnType("datetime2");
            });
            modelBuilder.Entity<NewsletterSetting>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.SmtpHost).HasMaxLength(200);
            });
            modelBuilder.Entity<SiteSetting>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.LogoPath).HasMaxLength(500);
                e.Property(x => x.FreeDeliveryThreshold).HasPrecision(18, 2);
                e.Property(x => x.FaviconPath).HasMaxLength(500);
                e.Property(x => x.Phone).HasMaxLength(50);
                e.Property(x => x.Email).HasMaxLength(200);
                e.Property(x => x.EconomicCode).HasMaxLength(50);
                e.Property(x => x.PostalCode).HasMaxLength(50);
            });
            modelBuilder.Entity<BoxFinalInfo>(e =>
            {
                e.HasKey(x => x.Id);
                e.HasIndex(x => x.OrderId);
                e.HasIndex(x => x.BoxTitle);
                e.Property(x => x.FinalPrice).HasPrecision(18, 2);
                // هر جعبه فقط یک ردیف قیمت نهایی می‌تواند داشته باشد؛
                // بدون این قید، دابل‌کلیک روی دکمه ثبت وزن، ردیف تکراری می‌ساخت و
                // صفحه جزئیات سفارش مشتری (ToDictionary روی BoxTitle) با خطای ۵۰۰ می‌ترکید.
                e.HasIndex(x => new { x.OrderId, x.BoxTitle }).IsUnique();
            });
            modelBuilder.Entity<MenuItem>(e =>
            {
                e.HasKey(x => x.Id);
                e.HasIndex(x => x.Location);
                e.HasIndex(x => x.ParentId);
                e.Property(x => x.Title).HasMaxLength(200);
                e.Property(x => x.Url).HasMaxLength(500);
            });
            modelBuilder.Entity<BirthdayReminder>(entity =>
            {
                entity.ToTable("BirthdayReminders");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.BirthDate);
                entity.Property(e => e.FirstName).HasMaxLength(100).IsRequired();
                entity.Property(e => e.LastName).HasMaxLength(100).IsRequired();
                entity.Property(e => e.Gender).HasMaxLength(10);
                entity.Property(e => e.Relation).HasMaxLength(100);
                entity.Property(e => e.Email).HasMaxLength(200);
                entity.Property(e => e.PhoneNumber).HasMaxLength(20);
                entity.Property(e => e.Notes).HasMaxLength(500);
                entity.Property(e => e.RemindDaysBefore).HasDefaultValue(3);
            });
            modelBuilder.Entity<CustomCakeOrder>(e =>
            {
                e.ToTable("CustomCakeOrders");
                e.HasKey(x => x.Id);
                e.HasIndex(x => x.UserId);
                e.HasIndex(x => x.Status);
                e.Property(x => x.Flavor).HasMaxLength(100);
                e.Property(x => x.Shape).HasMaxLength(100);
                e.Property(x => x.Occasion).HasMaxLength(200);
                e.Property(x => x.SampleImagePath).HasMaxLength(500);
                e.Property(x => x.PrintImagePath).HasMaxLength(500);
                e.Property(x => x.AdminNotes).HasMaxLength(1000);
                e.Property(x => x.SpecialRequests).HasMaxLength(2000);
                e.Property(x => x.FinalPrice).HasPrecision(18, 2);
                e.Property(x => x.DeliveryFee).HasPrecision(18, 2);
            });
            modelBuilder.Entity<ContactMessage>(e =>
            {
                e.ToTable("ContactMessages");
                e.HasKey(x => x.Id);
                e.HasIndex(x => x.Email);
                e.HasIndex(x => x.IsRead);
                e.HasIndex(x => x.CreatedAt);
                e.Property(x => x.FullName).HasMaxLength(100).IsRequired();
                e.Property(x => x.Email).HasMaxLength(200).IsRequired();
                e.Property(x => x.Phone).HasMaxLength(20);
                e.Property(x => x.Subject).HasMaxLength(200).IsRequired();
                e.Property(x => x.Message).HasMaxLength(2000).IsRequired();
            });

            // ── سامانه پیامکی ──
            // SmsLogs و SmsTemplates از قبل در دیتابیس وجود دارند؛ نگاشت دقیق به همان ستون‌ها
            modelBuilder.Entity<SmsLog>(e =>
            {
                e.ToTable("SmsLogs");
                e.HasKey(x => x.Id);
                e.Property(x => x.PhoneNumber).HasMaxLength(900).IsRequired();
                e.Property(x => x.MessageText).IsRequired();
                e.Property(x => x.Cost).HasColumnType("decimal(18,4)");
                // ستون‌های اختیاری ردیابی تحویل (با جدول موجود سازگارند)
                e.Property(x => x.ErrorMessage).HasMaxLength(500);
                e.HasIndex(x => x.PhoneNumber);
                e.HasIndex(x => x.SentAt);
                e.HasIndex(x => new { x.Status, x.SentAt });
            });
            modelBuilder.Entity<SmsTemplate>(e =>
            {
                e.ToTable("SmsTemplates");
                e.HasKey(x => x.Id);
                e.Property(x => x.Title).IsRequired();
                e.Property(x => x.BodyText).HasMaxLength(2000).IsRequired();
                e.HasIndex(x => x.Scenario);
            });
            modelBuilder.Entity<SmsSystemSetting>(e =>
            {
                e.ToTable("SmsSystemSettings");
                e.HasKey(x => x.Id);
                e.Property(x => x.ApiKey).HasMaxLength(1000);
                e.Property(x => x.SenderNumber).HasMaxLength(100);
                e.Property(x => x.NewOrderAlertRoles).HasMaxLength(200);
                e.Property(x => x.NewOrderAlertPhones).HasMaxLength(1000);
                e.Property(x => x.CustomCakeAlertPhones).HasMaxLength(1000);
                e.Property(x => x.LowStockAlertPhones).HasMaxLength(1000);
            });
            modelBuilder.Entity<SmsOtpCode>(e =>
            {
                e.ToTable("SmsOtpCodes");
                e.HasKey(x => x.Id);
                e.Property(x => x.Phone).HasMaxLength(20).IsRequired();
                // کد به‌صورت محافظت‌شده (Data Protection) ذخیره می‌شود؛ پس طول ستون بزرگ‌تر است
                e.Property(x => x.Code).HasMaxLength(512).IsRequired();
                e.Property(x => x.Purpose).HasMaxLength(20);
                e.Property(x => x.IpAddress).HasMaxLength(50);
                e.HasIndex(x => new { x.Phone, x.Purpose, x.IsUsed });
                e.HasIndex(x => x.ExpiresAt);
            });
            modelBuilder.Entity<RestockSubscription>(e =>
            {
                e.ToTable("RestockSubscriptions");
                e.HasKey(x => x.Id);
                e.HasIndex(x => new { x.SweetItemId, x.Notified });
                e.HasIndex(x => x.UserId);
            });
            modelBuilder.Entity<SmsAutoReminderLog>(e =>
            {
                e.ToTable("SmsAutoReminderLogs");
                e.HasKey(x => x.Id);
                e.Property(x => x.RefKey).HasMaxLength(100).IsRequired();
                e.Property(x => x.Phone).HasMaxLength(20).IsRequired();
                e.HasIndex(x => new { x.ReminderType, x.RefKey }).IsUnique();
            });
            modelBuilder.Entity<SmsCampaign>(e =>
            {
                e.ToTable("SmsCampaigns");
                e.HasKey(x => x.Id);
                e.Property(x => x.Title).HasMaxLength(200).IsRequired();
                e.Property(x => x.Message).IsRequired();
                e.HasIndex(x => x.Status);
            });
            modelBuilder.Entity<SmsOutboxItem>(e =>
            {
                e.ToTable("SmsOutboxItems");
                e.HasKey(x => x.Id);
                e.Property(x => x.Phone).HasMaxLength(20).IsRequired();
                e.Property(x => x.Message).IsRequired();
                e.Property(x => x.RefKey).HasMaxLength(100);
                e.HasIndex(x => new { x.Status, x.Id });
                e.HasIndex(x => x.CreatedAt);
            });
            modelBuilder.Entity<SmsCampaignRecipient>(e =>
            {
                e.ToTable("SmsCampaignRecipients");
                e.HasKey(x => x.Id);
                e.HasIndex(x => x.CampaignId);
                e.Property(x => x.Phone).HasMaxLength(20).IsRequired();
                e.Property(x => x.RecipientName).HasMaxLength(200);
                e.HasOne(x => x.Campaign)
                    .WithMany(c => c.Recipients)
                    .HasForeignKey(x => x.CampaignId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}