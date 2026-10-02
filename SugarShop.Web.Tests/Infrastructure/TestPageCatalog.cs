namespace SugarShop.Web.Tests.Infrastructure;

/// <summary>
/// پوسته‌ای که صفحه با آن رندر می‌شود. آزمون‌های مربوط به هدر/نوار پایینِ سایت فقط روی
/// صفحه‌های <see cref="Site"/> معنا دارند؛ پنل مدیریت و سرآشپز چیدمان خودشان را دارند و
/// صفحه‌های مستقل (بدون چیدمان) هیچ هدری ندارند.
/// </summary>
public enum PageShell
{
    /// <summary>چیدمان اصلی سایت (<c>_Layout</c>): هدر سایت + فوتر + نوار پایین اپ.</summary>
    Site,

    /// <summary>چیدمان پنل مدیریت (<c>_AdminLayout</c>).</summary>
    Admin,

    /// <summary>چیدمان داشبورد سرآشپز (<c>_ChefLayout</c>).</summary>
    Chef,

    /// <summary>سند مستقل بدون چیدمان (مثل صفحه دانلود اپ یا صورت‌حساب پیامکی).</summary>
    Standalone
}

/// <summary>یک صفحه‌ی قابل آزمون: نام فارسی برای گزارش‌ها + آدرس + پوسته‌ی صفحه.</summary>
public sealed record TestPage(string Name, string Url, PageShell Shell = PageShell.Site);

/// <summary>
/// فهرست صفحه‌هایی که آزمون جداسازی «اپ / وب» روی آن‌ها اجرا می‌شود.
/// اگر صفحه‌ی تازه‌ای با رندر شرطی اپ/وب اضافه شد، همین‌جا هم اضافه شود تا آزمون آن را بپوشاند.
/// </summary>
public static class TestPageCatalog
{
    public static readonly IReadOnlyList<TestPage> Pages = new List<TestPage>
    {
        new("صفحه اصلی", "/"),
        new("درباره ما", "/Home/Aboutus"),
        new("حریم خصوصی", "/Home/Privacy"),
        new("همه دسته‌بندی‌ها", "/Category/All"),
        new("فهرست شیرینی‌ها", "/SweetItem"),
        new("سبد خرید", "/Cart"),
        new("ورود", "/Account/Login"),
        new("دانلود اپلیکیشن", "/App/Download", PageShell.Standalone),
        new("داشبورد کاربری", "/Profile/Index"),
        new("سفارشات من", "/Profile/Orders"),
        new("کیف پول", "/Profile/Wallet"),
        new("ثبت کیک سفارشی", "/CustomCakeOrder/Create"),
        new("پنل مدیریت", "/Admin", PageShell.Admin),
        new("سفارش‌های پنل مدیریت", "/Admin/Orders", PageShell.Admin),
        new("داشبورد سرآشپز", "/Chef", PageShell.Chef)
    };

    /// <summary>صفحه‌هایی که با چیدمان اصلی سایت رندر می‌شوند (برای آزمون‌های هدر و نوار پایین).</summary>
    public static IEnumerable<TestPage> SiteShellPages
        => Pages.Where(p => p.Shell == PageShell.Site);

    /// <summary>صفحه‌هایی که در هر دو حالت نوار پایین دارند (سایت).</summary>
    public static IEnumerable<TestPage> PagesWithBottomNav
        => Pages.Where(p => p.Shell == PageShell.Site);

    /// <summary>
    /// صفحه‌های پنل مدیریت/سرآشپز: پوسته‌ی اپ (نوار پایین) فقط داخل اپ روی آن‌ها سوار می‌شود،
    /// چون در وب ناوبری اختصاصی خودشان را دارند.
    /// </summary>
    public static IEnumerable<TestPage> BackofficePages
        => Pages.Where(p => p.Shell is PageShell.Admin or PageShell.Chef);
}
