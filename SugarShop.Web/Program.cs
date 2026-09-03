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
using Microsoft.AspNetCore.DataProtection;
using System.IO;

var builder = WebApplication.CreateBuilder(args);

// ✅ Data Protection - ذخیره کلیدها در فایل سیستم
var dataProtectionPath = Path.Combine(builder.Environment.ContentRootPath, "DataProtectionKeys");
Directory.CreateDirectory(dataProtectionPath);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionPath))
    .SetApplicationName("SugarShop");

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("Connection string 'DefaultConnection' is either null, empty, or contains only whitespace. Please check your appsettings.json.");
}

// ✅ تغییر از MemoryCache به SqlServerCache برای Session
builder.Services.AddDistributedSqlServerCache(options =>
{
    options.ConnectionString = connectionString;
    options.SchemaName = "dbo";
    options.TableName = "AppSessions";
    options.DefaultSlidingExpiration = TimeSpan.FromHours(2);
});

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(2);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    // ✅ تنظیم SecurePolicy برای HTTPS
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
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

// ✅ تنظیمات Cookie Identity
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromHours(2);
    options.SlidingExpiration = true;
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    // ✅ تنظیم SameSite و SecurePolicy
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;

    // ✅ جلوگیری از لاگ‌اوت Owner
    options.Events.OnSigningIn = context =>
    {
        var isStaff = context.Principal?.IsInRole("Admin") == true ||
                      context.Principal?.IsInRole("OrderManager") == true ||
                      context.Principal?.IsInRole("Owner") == true ||
                      context.Principal?.IsInRole("Chef") == true;
        if (isStaff)
        {
            context.Properties.IsPersistent = false;
            context.Properties.ExpiresUtc = null;
            context.CookieOptions.Expires = null;
            context.CookieOptions.MaxAge = null;
        }
        return Task.CompletedTask;
    };
});

builder.Services.AddTransient<IEmailSender, EmailSender>();
builder.Services.Configure<NewsletterSetting>(builder.Configuration.GetSection("NewsletterSettings"));

builder.Services.AddControllersWithViews(options =>
{
    options.ModelBinderProviders.Insert(0, new PersianDateModelBinderProvider());
});

var app = builder.Build();

// ✅ ایجاد جدول Session در دیتابیس
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();

    try
    {
        // ایجاد جدول Session اگر وجود ندارد
        using var connection = new Microsoft.Data.SqlClient.SqlConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[AppSessions]') AND type in (N'U'))
            BEGIN
                CREATE TABLE [dbo].[AppSessions] (
                    [Id] nvarchar(900) NOT NULL,
                    [Value] varbinary(MAX) NOT NULL,
                    [ExpiresAtTime] datetimeoffset NOT NULL,
                    [SlidingExpirationInSeconds] bigint NULL,
                    [AbsoluteExpiration] datetimeoffset NULL,
                    PRIMARY KEY ([Id])
                );
                
                CREATE INDEX [Index_ExpiresAtTime] ON [dbo].[AppSessions] ([ExpiresAtTime]);
            END";
        command.ExecuteNonQuery();
        logger.LogInformation("Session table created or already exists.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error creating session table.");
    }
}

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
        await SeedRolesAndUsersAsync(roleManager, userManager, builder.Configuration, logger);
        await SeedPaymentGatewaysAsync(salesDb, builder.Configuration, logger);
        await SeedWalletSettingsAsync(salesDb);
        logger.LogInformation("Seed data inserted successfully.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error during database migration or seeding.");
    }
}

try
{
    using (var scope = app.Services.CreateScope())
    {
        var scheduler = scope.ServiceProvider.GetRequiredService<ContentSchedulerService>();
        await scheduler.ScheduleContentFetching();
    }
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "Error during Hangfire content scheduler startup. The site will continue without scheduled jobs.");
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

static async Task SeedRolesAndUsersAsync(RoleManager<IdentityRole> roleManager, UserManager<ApplicationUser> userManager, IConfiguration configuration, ILogger logger)
{
    string[] roles = { "Admin", "OrderManager", "User", "Owner", "Chef" };
    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
            await roleManager.CreateAsync(new IdentityRole(role));
    }

    async Task TryCreateUserAsync(string userName, string email, string fullName, string passwordKey, IEnumerable<string> userRoles)
    {
        var existing = await userManager.FindByNameAsync(userName);
        if (existing != null) return;

        var password = configuration[$"SeedPasswords:{passwordKey}"];
        if (string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("Seed password '{PasswordKey}' is not configured. Skipping creation of user '{UserName}'.", passwordKey, userName);
            return;
        }

        var user = new ApplicationUser
        {
            UserName = userName,
            Email = email,
            EmailConfirmed = true,
            FullName = fullName
        };
        var createResult = await userManager.CreateAsync(user, password);
        if (createResult.Succeeded)
        {
            await userManager.AddToRolesAsync(user, userRoles);
        }
        else
        {
            logger.LogWarning("Could not seed user '{UserName}': {Errors}", userName, string.Join(" | ", createResult.Errors.Select(e => e.Description)));
        }
    }

    await TryCreateUserAsync("SoransoftOWNER", "owner@soransoftpro.ir", "مالک سیستم", "Owner", new[] { "Owner", "Admin" });
    await TryCreateUserAsync("admin", "admin@sugarshop.com", "مدیر سیستم", "Admin", new[] { "Admin" });
    await TryCreateUserAsync("manager", "manager@sugarshop.com", "مدیر سفارشات", "Manager", new[] { "OrderManager" });
    await TryCreateUserAsync("chef", "chef@sugarshop.com", "سرآشپز", "Chef", new[] { "Chef" });
}

static async Task SeedPaymentGatewaysAsync(SugarShopSalesDbContext salesDb, IConfiguration configuration, ILogger logger)
{
    // درگاه زیبال: درگاه آنلاین پیش‌فرض پروژه (کلید پذیرنده از پنل مدیریت ثبت می‌شود)
    if (!await salesDb.PaymentGateways.AnyAsync(g => g.GatewayType == "Zibal"))
    {
        salesDb.PaymentGateways.Add(new PaymentGateway
        {
            Name = "Zibal",
            Title = "زیبال",
            GatewayType = "Zibal",
            IsActive = true,
            SortOrder = 1,
            CreatedAt = DateTime.UtcNow
        });
        await salesDb.SaveChangesAsync();
    }

    if (await salesDb.PaymentGateways.AnyAsync(g => g.GatewayType == "ZarinPal")) return;

    var zarinpalGateway = new PaymentGateway
    {
        Name = "ZarinPal",
        Title = "زرین‌پال",
        GatewayType = "ZarinPal",
        IsActive = true,
        SortOrder = 2,
        CreatedAt = DateTime.UtcNow
    };
    salesDb.PaymentGateways.Add(zarinpalGateway);
    await salesDb.SaveChangesAsync();

    var merchantId = configuration["Zarinpal:MerchantId"];
    if (string.IsNullOrWhiteSpace(merchantId))
    {
        logger.LogWarning("Zarinpal MerchantId is not configured. Payment gateway record was created without an account.");
        return;
    }

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
