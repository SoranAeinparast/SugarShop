using Hangfire;
using Hangfire.Dashboard;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using SugarShop.Domain.Entities;
using SugarShop.Domain.Entities.Newsletter;
using SugarShop.Domain.Entities.Sales;
using SugarShop.Infrastructure.Persistence;
using SugarShop.Infrastructure.Persistence.Sales;
using SugarShop.Infrastructure.Persistence.Seed;
using SugarShop.Infrastructure.Services;
using SugarShop.Web.ModelBinders;
using SugarShop.Web.Services;
using SugarShop.Web.Services.Implementations;
using SugarShop.Web.Services.Interfaces;
using SugarShop.Web.Validators;
using SugarShop.Web.ViewComponents;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// استفاده از IsNullOrWhiteSpace به جای چک کردن فقط null
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("Connection string 'DefaultConnection' is either null, empty, or contains only whitespace. Please check your appsettings.json.");
}


builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(2);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddHttpClient();
builder.Services.AddDatabaseDeveloperPageExceptionFilter();
builder.Services.AddScoped<ThemeStylesViewComponent>();
builder.Services.AddDbContext<SugarShopIdentityDbContext>(options =>
    options.UseSqlServer(connectionString, sql => sql.MigrationsAssembly("SugarShop.Infrastructure")));

builder.Services.AddDbContext<SugarShopCatalogDbContext>(options =>
    options.UseSqlServer(connectionString, sql => sql.MigrationsAssembly("SugarShop.Infrastructure")));

builder.Services.AddDbContext<SugarShopSalesDbContext>(options =>
    options.UseSqlServer(connectionString, sql => sql.MigrationsAssembly("SugarShop.Infrastructure")));

builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(connectionString));

builder.Services.AddHangfireServer();
builder.Services.AddScoped<IWebScraperService, WebScraperService>();
builder.Services.AddScoped<IContentParserService, ContentParserService>();
builder.Services.AddScoped<IAIContentService, AIContentService>();
builder.Services.AddScoped<IContentStorageService, ContentStorageService>();
builder.Services.AddScoped<IContentOrchestratorService, ContentOrchestratorService>();
builder.Services.AddScoped<ContentSchedulerService>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Cookie.MaxAge = null;
    options.ExpireTimeSpan = TimeSpan.FromHours(2);
    options.SlidingExpiration = true;
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

builder.Services.AddHttpClient<ZibalPaymentService>();
builder.Services.AddScoped<ZibalPaymentService>();
builder.Services.AddScoped<ZarinPalPaymentService>();
builder.Services.AddScoped<InventoryService>();

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.User.RequireUniqueEmail = true;
    options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+ \u0600-\u06FF\uFB50-\uFDFF\uFE70-\uFEFF";
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
    options.SignIn.RequireConfirmedEmail = false;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
})
.AddEntityFrameworkStores<SugarShopIdentityDbContext>()
.AddDefaultTokenProviders();

builder.Services.AddTransient<IEmailSender, EmailSender>();
builder.Services.Configure<NewsletterSetting>(
    builder.Configuration.GetSection("NewsletterSettings"));

builder.Services.AddControllersWithViews(options =>
{
    options.ModelBinderProviders.Insert(0, new PersianDateModelBinderProvider());
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    try
    {
        var catalogDb = services.GetRequiredService<SugarShopCatalogDbContext>();
        var salesDb = services.GetRequiredService<SugarShopSalesDbContext>();
        var identityDb = services.GetRequiredService<SugarShopIdentityDbContext>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        await catalogDb.Database.MigrateAsync();
        await salesDb.Database.MigrateAsync();
        await identityDb.Database.MigrateAsync();
        logger.LogInformation("Database migrated successfully.");
        await CategorySeeder.SeedAsync(catalogDb);
        await SliderSeeder.SeedAsync(catalogDb);
        await SweetItemSeeder.SeedAsync(catalogDb);
        await BoxTypeSeeder.SeedAsync(catalogDb);
        await SeedRolesAndUsersAsync(roleManager, userManager, builder.Configuration);
        await SeedPaymentGatewaysAsync(salesDb, builder.Configuration);
        await SeedWalletSettingsAsync(salesDb);
        logger.LogInformation("Seed data inserted successfully.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error during database migration or seeding.");
    }
}

using (var scope = app.Services.CreateScope())
{
    var scheduler = scope.ServiceProvider.GetRequiredService<ContentSchedulerService>();
    scheduler.ScheduleContentFetching();
}

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireDashboardAuthorizationFilter() }
});

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

static async Task SeedRolesAndUsersAsync(RoleManager<IdentityRole> roleManager, UserManager<ApplicationUser> userManager, IConfiguration configuration)
{
    string[] roles = { "Admin", "OrderManager", "User", "Owner" };
    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
            await roleManager.CreateAsync(new IdentityRole(role));
    }

    var ownerUserName = "SoransoftOWNER";
    var ownerUser = await userManager.FindByNameAsync(ownerUserName);
    if (ownerUser == null)
    {
        var ownerPassword = configuration["SeedPasswords:Owner"]
            ?? throw new InvalidOperationException("Seed password 'Owner' not configured.");
        ownerUser = new ApplicationUser
        {
            UserName = ownerUserName,
            Email = "owner@soransoftpro.ir",
            EmailConfirmed = true,
            FullName = "مالک سیستم"
        };
        var createResult = await userManager.CreateAsync(ownerUser, ownerPassword);
        if (createResult.Succeeded)
        {
            await userManager.AddToRolesAsync(ownerUser, new[] { "Owner", "Admin" });
        }
    }

    var adminUser = await userManager.FindByNameAsync("admin");
    if (adminUser == null)
    {
        var adminPassword = configuration["SeedPasswords:Admin"]
            ?? throw new InvalidOperationException("Seed password 'Admin' not configured.");
        adminUser = new ApplicationUser { UserName = "admin", Email = "admin@sugarshop.com", EmailConfirmed = true, FullName = "مدیر سیستم" };
        await userManager.CreateAsync(adminUser, adminPassword);
        await userManager.AddToRoleAsync(adminUser, "Admin");
    }

    var managerUser = await userManager.FindByNameAsync("manager");
    if (managerUser == null)
    {
        var managerPassword = configuration["SeedPasswords:Manager"]
            ?? throw new InvalidOperationException("Seed password 'Manager' not configured.");
        managerUser = new ApplicationUser { UserName = "manager", Email = "manager@sugarshop.com", EmailConfirmed = true, FullName = "مدیر سفارشات" };
        await userManager.CreateAsync(managerUser, managerPassword);
        await userManager.AddToRoleAsync(managerUser, "OrderManager");
    }
}

static async Task SeedPaymentGatewaysAsync(SugarShopSalesDbContext salesDb, IConfiguration configuration)
{
    if (await salesDb.PaymentGateways.AnyAsync()) return;

    var zarinpalGateway = new PaymentGateway
    {
        Name = "ZarinPal",
        Title = "زرین‌پال",
        GatewayType = "ZarinPal",
        IsActive = true,
        SortOrder = 1,
        CreatedAt = DateTime.UtcNow
    };
    salesDb.PaymentGateways.Add(zarinpalGateway);
    await salesDb.SaveChangesAsync();

    var merchantId = configuration["Zarinpal:MerchantId"]
        ?? throw new InvalidOperationException("Zarinpal MerchantId not configured.");

    var isTest = configuration["Zarinpal:ZarinpalMode"] != "Production";

    salesDb.PaymentGatewayAccounts.Add(new PaymentGatewayAccount
    {
        GatewayId = zarinpalGateway.Id,
        Title = "حساب اصلی زرین‌پال",
        IsActive = true,
        ConfigData = JsonSerializer.Serialize(new { MerchantId = merchantId, IsTestAccount = isTest }),
        CreatedAt = DateTime.UtcNow
    });
    await salesDb.SaveChangesAsync();
}

static async Task SeedWalletSettingsAsync(SugarShopSalesDbContext salesDb)
{
    if (await salesDb.WalletSettings.AnyAsync()) return;

    salesDb.WalletSettings.Add(new WalletSettings
    {
        IsEnabled = true,
        ReturnType = "Percentage",
        ReturnValue = 0,
        MinimumOrderAmount = 0,
        AllowDirectRecharge = true,
        DirectRechargeMinAmount = 10000,
        DirectRechargeMaxAmount = 0,
        MaxWalletBalance = 0,
        ExpiryDays = 0,
        UpdatedAt = DateTime.UtcNow
    });
    await salesDb.SaveChangesAsync();
}

public class HangfireDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        return httpContext.User.Identity?.IsAuthenticated == true
            && (httpContext.User.IsInRole("Admin") || httpContext.User.IsInRole("Owner"));
    }
}
