using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SugarShop.Domain.Entities;
using SugarShop.Domain.Entities.Sales;
using SugarShop.Infrastructure.Persistence;
using SugarShop.Infrastructure.Persistence.Sales;

namespace SugarShop.Web.Tests.Infrastructure;

/// <summary>
/// میزبان واقعی برنامه در حافظه برای آزمون‌ها.
///
/// • محیط اجرا «Testing» است؛ بنابراین حصار داخل <c>Program.cs</c> مانع از اجرای کارهای
///   پس‌زمینه می‌شود: نه سرور Hangfire، نه پردازش صف پیامک (هیچ پیامک واقعی ارسال نمی‌شود)،
///   نه مهاجرت/seed روی دیتابیس واقعی و نه ثبت کارهای زمان‌بندی‌شده.
/// • هر سه DbContext با نسخه‌ی in-memory جایگزین می‌شوند تا آزمون به SQL Server وابسته نباشد
///   و هیچ داده‌ای در دیتابیس توسعه دست‌کاری نشود.
/// • کش توزیع‌شده (که در برنامه SQL Server است) با نسخه‌ی حافظه‌ای جایگزین می‌شود.
/// • ورود کاربر با یک اسکیم احراز هویت آزمون انجام می‌شود (بدون کوکی و بدون OTP).
/// </summary>
public class AppModeWebFactory : WebApplicationFactory<global::Program>
{
    /// <summary>User-Agent پوسته‌ی اندرویدی سایت — تنها نشانه‌ی «داخل اپ» در سمت سرور.</summary>
    public const string AppUserAgent =
        "Mozilla/5.0 (Linux; Android 13; SM-A536B) AppleWebKit/537.36 (KHTML, like Gecko) " +
        "Version/4.0 Chrome/120.0.0.0 Mobile Safari/537.36 SugarShopApp/1.4";

    /// <summary>User-Agent مرورگر دسکتاپ — «وب».</summary>
    public const string BrowserUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) " +
        "Chrome/121.0.0.0 Safari/537.36";

    private readonly SemaphoreSlim _seedGate = new(1, 1);
    private bool _seeded;

    /// <summary>
    /// پسوند یکتا برای نام دیتابیس‌های in-memory: هر میزبان آزمون داده‌ی خودش را دارد و
    /// آزمون‌هایی که موازی اجرا می‌شوند روی هم اثر نمی‌گذارند.
    /// </summary>
    private readonly string _databaseSuffix = Guid.NewGuid().ToString("N")[..8];

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // برنامه بدون رشته اتصال بالا نمی‌آید؛ آزمون به آن وصل نمی‌شود (DbContextها in-memory‌اند)
        // ولی نام دیتابیس صریحاً «آزمون» است تا هیچ‌وقت با دیتابیس واقعی اشتباه گرفته نشود.
        builder.UseSetting("ConnectionStrings:DefaultConnection",
            @"Server=(localdb)\SugarShopAutomatedTests;Database=SugarShopAutomatedTests;Trusted_Connection=True;TrustServerCertificate=True");
        // نام پکیج اپ لازم است تا نوار «باز کردن در اپلیکیشن» رندر شود و آزمون بتواند جداسازی
        // بخش اپ‌محور/وب‌محور آن را هم بسنجد.
        builder.UseSetting("Twa:PackageName", "ir.soranshop.autotest");

        builder.ConfigureTestServices(services =>
        {
            ReplaceWithInMemory<SugarShopSalesDbContext>(services, $"autotest-sales-{_databaseSuffix}");
            ReplaceWithInMemory<SugarShopCatalogDbContext>(services, $"autotest-catalog-{_databaseSuffix}");
            ReplaceWithInMemory<SugarShopIdentityDbContext>(services, $"autotest-identity-{_databaseSuffix}");

            // سشن/کش در برنامه روی SQL Server است؛ در آزمون حافظه‌ای می‌شود
            services.RemoveAll<IDistributedCache>();
            services.AddDistributedMemoryCache();

            services.AddAuthentication(options =>
            {
                options.DefaultScheme = TestAuthHandler.SchemeName;
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
            }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        });
    }

    private static void ReplaceWithInMemory<TContext>(IServiceCollection services, string databaseName)
        where TContext : DbContext
    {
        foreach (var descriptor in services
                     .Where(d => d.ServiceType == typeof(TContext) || d.ServiceType == typeof(DbContextOptions<TContext>))
                     .ToList())
        {
            services.Remove(descriptor);
        }

        services.AddDbContext<TContext>(options => options.UseInMemoryDatabase(databaseName));
    }

    /// <summary>یک کلاینت با User-Agent مشخص می‌سازد (و دیتابیس آزمون را یک‌بار آماده می‌کند).</summary>
    public async Task<HttpClient> CreatePageClientAsync(string userAgent)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
        await EnsureSeededAsync();
        client.DefaultRequestHeaders.UserAgent.Clear();
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", userAgent);
        return client;
    }

    /// <summary>شناسه‌ی سفارش آزمون (برای صفحه‌ی جزئیات و سند صورت‌حساب).</summary>
    public int SampleOrderId { get; private set; }

    /// <summary>توکن اختصاصی صورت‌حساب همان سفارش (مسیر <c>/s/{token}</c>).</summary>
    public string? StatementToken { get; private set; }

    /// <summary>داده‌های حداقلی آزمون: کاربر تثبیتی + تنظیمات قالب/سایت (یک‌بار برای هر میزبان).</summary>
    public async Task EnsureSeededAsync()
    {
        if (_seeded) return;
        await _seedGate.WaitAsync();
        try
        {
            if (_seeded) return;

            using var scope = Services.CreateScope();
            var provider = scope.ServiceProvider;

            await SeedIdentityAsync(provider);
            await SeedSalesSettingsAsync(provider);
            await SeedCatalogAsync(provider);
            await SeedStatementOrderAsync(provider);

            _seeded = true;
        }
        finally
        {
            _seedGate.Release();
        }
    }

    private static async Task SeedIdentityAsync(IServiceProvider provider)
    {
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = provider.GetRequiredService<RoleManager<IdentityRole>>();

        foreach (var role in TestAuthHandler.Roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        var existing = await userManager.FindByIdAsync(TestAuthHandler.UserId);
        if (existing == null)
        {
            var user = new ApplicationUser
            {
                Id = TestAuthHandler.UserId,
                UserName = "autotest-user",
                Email = "autotest@sugarshop.local",
                PhoneNumber = "09000000000",
                EmailConfirmed = true,
                PhoneNumberConfirmed = true,
                FullName = TestAuthHandler.FullName
            };
            var created = await userManager.CreateAsync(user);
            if (!created.Succeeded)
                throw new InvalidOperationException("ساخت کاربر آزمون ناموفق بود: " +
                    string.Join(" | ", created.Errors.Select(e => e.Description)));

            foreach (var role in TestAuthHandler.Roles)
                await userManager.AddToRoleAsync(user, role);
        }
    }

    /// <summary>
    /// یک سفارش وزن‌کشی‌شده‌ی حداقلی + توکن صورت‌حساب می‌سازد تا صفحه‌های مستقل
    /// «صورت‌حساب پیامکی» و «جزئیات سفارش» هم در آزمون جداسازی اپ/وب پوشش داده شوند.
    /// </summary>
    private async Task SeedStatementOrderAsync(IServiceProvider provider)
    {
        var salesDb = provider.GetRequiredService<SugarShopSalesDbContext>();
        var statementLinks = provider.GetRequiredService<SugarShop.Web.Services.StatementLinkService>();

        var order = await salesDb.Orders.FirstOrDefaultAsync(o => o.OrderCode == "AUTOTEST-0001");
        if (order == null)
        {
            order = new Order
            {
                OrderCode = "AUTOTEST-0001",
                UserId = TestAuthHandler.UserId,
                OrderStatus = OrderStatus.PendingPayment,
                PaymentStatus = PaymentStatus.Unpaid,
                IsPaymentEnabled = true,
                TotalAmountSnapshot = 250000m,
                DeliveryFeeSnapshot = 30000m,
                DeliveryMethod = DeliveryMethod.Delivery,
                CustomerName = TestAuthHandler.FullName,
                CustomerPhone = "09000000000",
                CustomerFullAddress = "تهران، خیابان آزمون",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            salesDb.Orders.Add(order);
            await salesDb.SaveChangesAsync();
        }

        SampleOrderId = order.Id;
        StatementToken = await statementLinks.GetOrCreateTokenAsync(order.Id, order.UserId);
    }

    private static async Task SeedCatalogAsync(IServiceProvider provider)
    {
        var catalogDb = provider.GetRequiredService<SugarShopCatalogDbContext>();

        // یک دسته‌بندی فعال تا ویترین صفحه اصلی و صفحه دسته‌بندی‌ها واقعاً رندر شوند
        // (آزمون جداسازی، حضور/غیاب ویترین وب‌محور را همانجا می‌سنجد).
        if (!await catalogDb.Categories.AnyAsync())
        {
            catalogDb.Categories.Add(new Category
            {
                TitleFa = "شیرینی آزمون",
                Slug = "autotest-sweets",
                IsActive = true,
                SortOrder = 1
            });
            await catalogDb.SaveChangesAsync();
        }
    }

    private static async Task SeedSalesSettingsAsync(IServiceProvider provider)
    {
        var salesDb = provider.GetRequiredService<SugarShopSalesDbContext>();

        if (!await salesDb.SiteSettings.AnyAsync())
        {
            salesDb.SiteSettings.Add(new SiteSetting { SiteTitle = "شیرینی سرای آزمون" });
        }

        if (!await salesDb.ThemeSettings.AnyAsync())
        {
            // هدر Fixed تا آزمون بتواند «class=header-fixed» را در وب ببیند و نبودش را در اپ بسنجد
            salesDb.ThemeSettings.Add(new ThemeSetting
            {
                HeaderType = HeaderType.Fixed,
                HeaderTransparency = 85,
                HeaderBgColor = "#4E342E",
                AppEnabled = true,
                AppDisplayName = "شیرینی سرای آزمون",
                // آدرس پایه‌ی ثابت تا لینک‌های داخل پیامک (توکن صورت‌حساب) در آزمون قطعی باشند
                AppBaseUrl = "https://shop.autotest.local"
            });
        }

        await salesDb.SaveChangesAsync();
    }
}
