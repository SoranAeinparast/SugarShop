using SugarShop.Domain.Entities.Sales;
using SugarShop.Web.Tests.Infrastructure;
using Xunit;

namespace SugarShop.Web.Tests;

/// <summary>
/// 🛡️ قفل‌کردن ستون نشانگر «سفارش داخلی» (<see cref="Order.IsInternal"/>).
///
/// این ستون جای «مقایسه‌ی متن <c>Notes</c>» را گرفته است تا فیلترها و جمع مبلغ‌های لیست سفارش‌ها
/// مجبور نباشند جدول پهن را (با ستون <c>Notes</c> از نوع max) بخوانند. برای اینکه این بهینه‌سازی
/// پایدار بماند، آزمون‌ها سه چیز را قفل می‌کنند:
///
///   ۱) مقدار نشانگر همیشه از همان قاعده‌ی قبلی ساخته می‌شود (و هر نوشتن روی Notes آن را به‌روز می‌کند)،
///   ۲) هیچ کوئری‌ای دیگر فیلتر «سفارش داخلی» را از روی متن Notes نمی‌گذارد،
///   ۳) مهاجرت، ستون را برای سفارش‌های موجود پر می‌کند و ایندکس پوشا را می‌سازد.
/// </summary>
public class OrderInternalMarkerTests
{
    // ─────────────────────────────────────────────────────────────
    // ۱) ستون نشانگر از قاعده‌ی Notes ساخته می‌شود
    // ─────────────────────────────────────────────────────────────
    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("لطفاً تا ساعت ۱۸ آماده باشد", false)]
    [InlineData("WalletRecharge", true)]          // شارژ کیف پول
    [InlineData("walletrecharge", true)]          // رفتار SQL قبلی هم غیرحساس به حروف بود
    [InlineData("WalletRechargeX", false)]        // برابری کامل، نه StartsWith
    [InlineData("CustomCakeOrder_12", true)]      // سفارش موقت کیک سفارشی
    [InlineData("customcakeorder_12", true)]
    [InlineData("CustomCake", false)]
    public void Marker_is_derived_from_the_notes_rule(string? notes, bool expectedInternal)
    {
        var order = new Order { Notes = notes };

        Assert.Equal(expectedInternal, order.IsInternal);
        Assert.Equal(expectedInternal, OrderNotes.IsInternalNotes(notes));
    }

    [Fact]
    public void Changing_notes_updates_the_marker_on_the_same_instance()
    {
        var order = new Order();

        order.Notes = OrderNotes.WalletRecharge;
        Assert.True(order.IsInternal);

        order.Notes = OrderNotes.ForCustomCakeOrder(9);
        Assert.True(order.IsInternal);

        order.Notes = "یادداشت عادی مشتری";
        Assert.False(order.IsInternal);

        order.Notes = null;
        Assert.False(order.IsInternal);
    }

    // ─────────────────────────────────────────────────────────────
    // ۲) فیلتر «سفارش داخلی» دیگر از متن Notes نمی‌آید
    // ─────────────────────────────────────────────────────────────
    [Fact]
    public void No_query_excludes_internal_orders_by_matching_the_notes_text()
    {
        // الگوهای قبلی:  o.Notes != "WalletRecharge"  و  (o.Notes == null || !o.Notes.StartsWith(...))
        var offenders = RepositoryLayout.WebFiles(".cs")
            .Where(file =>
            {
                var content = File.ReadAllText(file);
                return content.Contains("o.Notes != ", StringComparison.Ordinal)
                    || content.Contains("!o.Notes.StartsWith", StringComparison.Ordinal)
                    || content.Contains("o.Notes == null", StringComparison.Ordinal);
            })
            .Select(file => Path.GetRelativePath(RepositoryLayout.WebProjectDir, file).Replace('\\', '/'))
            .ToList();

        Assert.True(offenders.Count == 0,
            "فیلتر «سفارش داخلی» باید فقط از ستون نشانگر IsInternal بیاید، ولی این فایل‌ها هنوز " +
            "متن Notes را مقایسه می‌کنند (و همین باعث اسکن جدول پهن می‌شود): " + string.Join(" ، ", offenders));
    }

    [Fact]
    public void Admin_order_list_uses_the_indexed_marker()
    {
        var admin = File.ReadAllText(Path.Combine(RepositoryLayout.WebProjectDir, "Controllers", "AdminController.cs"));

        Assert.Contains("!o.IsInternal", admin, StringComparison.Ordinal);
        // ستون Notes نباید در کوئری لیست پنل ادمین ظاهر شود (نه فیلتر، نه انتخاب)
        Assert.DoesNotContain("o.Notes", admin, StringComparison.Ordinal);
    }

    // ─────────────────────────────────────────────────────────────
    // ۳) مهاجرت: پرکردن مقدار برای سفارش‌های موجود + ایندکس پوشا
    // ─────────────────────────────────────────────────────────────
    [Fact]
    public void Marker_migration_backfills_existing_orders_and_builds_the_covering_index()
    {
        var directory = Path.Combine(RepositoryLayout.Root, "SugarShop.Infrastructure", "Migrations", "SugarShopSalesDb");
        var migration = Directory.EnumerateFiles(directory, "*AddOrderIsInternalMarker.cs")
            .Single(path => !path.EndsWith(".Designer.cs", StringComparison.Ordinal));
        var content = File.ReadAllText(migration);

        // سفارش‌های موجود باید با همان قاعده‌ی قبلی علامت بخورند
        Assert.Contains("UPDATE [Orders] SET [IsInternal] = 1", content, StringComparison.Ordinal);
        Assert.Contains("WalletRecharge", content, StringComparison.Ordinal);
        Assert.Contains("CustomCakeOrder", content, StringComparison.Ordinal);

        // و ایندکس باید «پوشا» باشد تا شمارش/جمع مبلغ بدون Lookup حساب شود
        Assert.Contains("SqlServer:Include", content, StringComparison.Ordinal);
        Assert.Contains("FinalTotalAmount", content, StringComparison.Ordinal);
        Assert.Contains("TotalAmountSnapshot", content, StringComparison.Ordinal);
    }
}
