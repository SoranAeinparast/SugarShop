using Hangfire;
using Hangfire.Dashboard;
using Microsoft.AspNetCore.Authentication;
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
using SugarShop.Web.Services.Sms;
using SugarShop.Web.Validators;
using SugarShop.Web.ViewComponents;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.ResponseCompression;
using System.IO;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using SugarShop.Web.Services.Api;

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

// ── Web API داخلی (اتصال اپلیکیشن موبایل) ──
// توکن JWT روی همان Identity سایت صادر می‌شود؛ حساب کاربری مشترک وب + اپ.
// اسکیم JWT همیشه ثبت می‌شود (بدون کلید معتبر، توکن‌ها صادر/پذیرفته نمی‌شوند → ۴۰۱)
// تا Attributeهای Authorize در هیچ حالتی خطای «scheme not registered» ندهند.
builder.Services.AddSingleton<ApiJwtTokenService>();
// سرویس پیامک برای ساخت لینک مطلق داخل متن پیامک به آدرس همان درخواست نیاز دارد
builder.Services.AddHttpContextAccessor();
var apiSecret = ApiAuthKeyHolder.GetKey(builder.Configuration); // یک کلید برای هم امضا هم اعتبارسنجی
builder.Services.AddAuthentication()
    .AddJwtBearer("ApiJwt", options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["ApiAuth:Issuer"] ?? "SugarShop",
            ValidateAudience = true,
            ValidAudience = builder.Configuration["ApiAuth:Audience"] ?? "SugarShopMobile",
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(apiSecret)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            RoleClaimType = System.Security.Claims.ClaimTypes.Role,
            NameClaimType = "sub"
        };
    });
builder.Services.AddScoped<ThemeStylesViewComponent>();

builder.Services.AddDbContext<SugarShopIdentityDbContext>(options =>
    options.UseSqlServer(connectionString, sql => sql.MigrationsAssembly("SugarShop.Infrastructure")));

// ✅ اتصال SQL مشترک برای DbContextهای فروش و کاتالوگ:
// هر DbContext به‌صورت پیش‌فرض اتصال مستقل خودش را می‌سازد؛ در نتیجه نمی‌توان تراکنشی را که روی
// یک DbContext آغاز شده (BeginTransaction) با UseTransaction روی DbContext دیگر اعمال کرد و خطای
// «The specified transaction is not associated with the current connection» رخ می‌دهد.
// با تزریق یک SqlConnection مشترک (scoped) به هر دو DbContext، هر دو روی یک اتصال واحد کار می‌کنند
// و یک تراکنش می‌تواند هر دو DbContext را پوشش دهد (الگوی رسمی EF Core برای اشتراک تراکنش).
builder.Services.AddScoped(_ => new Microsoft.Data.SqlClient.SqlConnection(connectionString));
builder.Services.AddDbContext<SugarShopCatalogDbContext>((sp, options) =>
    options.UseSqlServer(sp.GetRequiredService<Microsoft.Data.SqlClient.SqlConnection>(),
        sql => sql.MigrationsAssembly("SugarShop.Infrastructure")));
builder.Services.AddDbContext<SugarShopSalesDbContext>((sp, options) =>
    options.UseSqlServer(sp.GetRequiredService<Microsoft.Data.SqlClient.SqlConnection>(),
        sql => sql.MigrationsAssembly("SugarShop.Infrastructure")));

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
builder.Services.AddScoped<SugarShop.Web.Services.OrderPricingService>();
    // تولید PDF صورتحساب پرداخت: بدون حالت (state) است و فونت‌هایش یک‌بار ثبت می‌شوند
    builder.Services.AddSingleton<SugarShop.Web.Services.PaymentStatementPdfService>();
    // توکن موقت لینک صورت‌حساب (باز شدن سند بدون نیاز به ورود)
    builder.Services.AddScoped<SugarShop.Web.Services.StatementLinkService>();
    builder.Services.AddScoped<SugarShop.Web.Services.ImageStorageService>();

// ✅ فشرده‌سازی پاسخ (Gzip/Brotli): حجم HTML/CSS/JS تا ۸۰٪ کم می‌شود — تأثیر بزرگ روی سرعت لود
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
    {
        "text/html",
        "text/plain",
        "text/css",
        "application/javascript",
        "application/json",
        "image/svg+xml",
        "font/woff2"
    });
});

// ── سامانه پیامکی ──
builder.Services.AddHttpClient<SmsIrClient>();
// صف پیامک ماندگار در دیتابیس (Outbox): با ری‌استارت شدن سایت هیچ پیامکی گم نمی‌شود
builder.Services.AddSingleton<ISmsQueue, DbSmsQueue>();
builder.Services.AddScoped<SmsService>();
// اثرسنجی پیامک‌های لینک‌دار (چه زمانی رفت / چند بار باز شد) + گزارش پنل ادمین
builder.Services.AddScoped<SugarShop.Web.Services.SmsLinkTrackingService>();
builder.Services.AddScoped<SugarShop.Web.Services.SmsLinkReportService>();
builder.Services.AddScoped<SmsJobs>();
// کار شبانه پاک‌سازی جدول‌های موقت (توکن صورت‌حساب، لاگ پیامک، صف، OTP و سشنهای منقضی)
builder.Services.AddScoped<SugarShop.Web.Services.MaintenanceJobs>();
builder.Services.AddHostedService<SmsProcessor>();

// تأیید ایمیل قابل تنظیم است (Identity:RequireConfirmedEmail). پیش‌فرض "false" است تا وقتی SMTP تنظیم نشده
// ورود کاربران فعلی قفل نشود؛ برای روشن‌کردن، مقدار را در appsettings به true تغییر دهید.
var requireConfirmedEmail = string.Equals(
    builder.Configuration["Identity:RequireConfirmedEmail"], "true", StringComparison.OrdinalIgnoreCase);

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.User.RequireUniqueEmail = true;
    options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+ \u0600-\u06FF\uFB50-\uFDFF\uFE70-\uFEFF";
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
    options.SignIn.RequireConfirmedEmail = requireConfirmedEmail;
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

    // ✅ ضمانت نهایی برای رفع باگ «کارمند بدون خروج، همچنان لاگین است»:
    // اگر کارمند (Owner/Admin/OrderManager/Chef) هنوز کوکی ماندگار (persistent) از نسخه‌های قبلی
    // داشته باشد که با بستن مرورگر حذف نمی‌شود، در اولین درخواست لاگ‌اوت می‌شود تا جلسه او
    // همیشه فقط به‌صورت Session (غیرماندگار) باشد. این باعث می‌شود کوکی‌های قدیمی هم باطل شوند.
    var defaultOnValidatePrincipal = options.Events.OnValidatePrincipal;
    options.Events.OnValidatePrincipal = async context =>
    {
        // اجرای اعتبارسنجی پیش‌فرض Identity (Security Stamp) ابتدا
        if (defaultOnValidatePrincipal != null)
        {
            await defaultOnValidatePrincipal(context);
        }

        if (context.Principal == null || context.Properties == null || !context.Properties.IsPersistent)
        {
            return;
        }

        bool isStaff = context.Principal.IsInRole("Admin") ||
                       context.Principal.IsInRole("OrderManager") ||
                       context.Principal.IsInRole("Owner") ||
                       context.Principal.IsInRole("Chef");
        if (isStaff)
        {
            // کارمند با کوکی ماندگار → باطل کردن نشست و حذف کوکی
            context.RejectPrincipal();
            context.ShouldRenew = false;
            await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        }
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
        await SmsSeeder.SeedAsync(scope.ServiceProvider.GetRequiredService<IServiceScopeFactory>(), logger);
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

// ── وظایف زمان‌بندی‌شده سامانه پیامکی (یادآوری تولد / بازگشت مشتری / هشدار موجودی) ──
try
{
    using (var scope = app.Services.CreateScope())
    {
        var smsSettings = await scope.ServiceProvider.GetRequiredService<SmsService>().GetSettingsAsync();
        SmsProcessor.UpdateThrottle(smsSettings.QueueBatchSize, smsSettings.QueueDelaySeconds);
    }
    // ۹ صبح به وقت ایران = ۵:۳۰ UTC
    RecurringJob.AddOrUpdate<SmsJobs>("sms-daily-reminders", j => j.RunDailyAsync(), "30 5 * * *");

    // یادآوری پرداخت سفارش‌های وزن‌کشی‌شده: هر ساعت در دقیقه ۲۰ (UTC) تا تأخیر تنظیم‌شده
    // (پیش‌فرض ۲۴ ساعت) دقیق رعایت شود، نه «حداکثر یک روز بعد».
    RecurringJob.AddOrUpdate<SmsJobs>("sms-payment-reminders", j => j.RunPaymentRemindersAsync(), "20 * * * *");
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "Error scheduling SMS recurring jobs. The site will continue without SMS jobs.");
}

// ── کار شبانه نگهداری دیتابیس (پاک‌سازی ردیف‌های منقضی و لاگ‌های قدیمی) ──
try
{
    // ۳ بامداد به وقت ایران = ۲۳:۳۰ UTC روز قبل
    RecurringJob.AddOrUpdate<SugarShop.Web.Services.MaintenanceJobs>(
        "nightly-cleanup", j => j.RunNightlyCleanupAsync(), "30 23 * * *");
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "Error scheduling the nightly maintenance job. The site continues without it.");
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

// ✅ فشرده‌سازی پاسخ — باید قبل از هر middleware دیگری که بدنه پاسخ می‌نویسد فعال شود
app.UseResponseCompression();

// ✅ کش مطمئن فایل‌های ایستا (تصاویر/CSS/JS): مرورگر یک هفته کش می‌کند — سرعت لود صفحات بعدی به‌طور محسوس بالا می‌رود
// APK اپ اندروید — نوع MIME آن در پیش‌فرض ASP.NET نیست؛ بدون این، دانلود اپ ۴۰۴ می‌شود
var staticContentTypeProvider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
staticContentTypeProvider.Mappings[".apk"] = "application/vnd.android.package-archive";

app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = staticContentTypeProvider,
    OnPrepareResponse = ctx =>
    {
        const int cacheDays = 7;
        // sw.js باید همیشه تازه باشد — در غیر این صورت به‌روزرسانی Service Worker تا ۲۴ ساعت تأخیر می‌خورد
        if (ctx.Context.Request.Path.StartsWithSegments("/sw.js"))
            ctx.Context.Response.Headers["Cache-Control"] = "no-cache";
        else
            ctx.Context.Response.Headers["Cache-Control"] = $"public,max-age={cacheDays * 24 * 3600}";
    }
});

// ✅ جلوگیری از کش‌شدن صفحات پویا (مثل تنظیمات هدر دسته‌بندی):
// بدون این هدر، مرورگر یا CDN نسخه قدیمی صفحه را نشان می‌داد و تغییرات اعمال‌نشده به نظر می‌رسیدند
app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        var response = context.Response;
        var contentType = response.ContentType ?? "";
        var isHtml = contentType.StartsWith("text/html", StringComparison.OrdinalIgnoreCase);
        // صفحات HTML عمومی: بدون کش؛ استاتیک‌ها و APIها مستثنی هستند (هدر خودشان را دارند)
        if (isHtml && !context.Request.Path.StartsWithSegments("/media"))
        {
            response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
            response.Headers["Pragma"] = "no-cache";
            response.Headers["Expires"] = "0";
        }
        return Task.CompletedTask;
    });
    await next();
});

app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireDashboardAuthorizationFilter() }
});

app.MapControllers();

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

    // ⚠️ زرین‌پال به فرآیند پرداخت این نسخه متصل نیست (تنها زیبال پیاده‌سازی شده است)؛
    // به همین دلیل از ابتدا غیرفعال ساخته میشود تا ادمین گمان نکند پرداخت از این درگاه انجام میشود.
    var zarinpalGateway = new PaymentGateway
    {
        Name = "ZarinPal",
        Title = "زرین‌پال",
        GatewayType = "ZarinPal",
        IsActive = false,
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
