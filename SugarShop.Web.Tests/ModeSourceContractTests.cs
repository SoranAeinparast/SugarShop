using SugarShop.Web.Tests.Infrastructure;
using Xunit;

namespace SugarShop.Web.Tests;

/// <summary>
/// 🛡️ آزمون‌های نگهبان روی خودِ فایل‌های منبع (نه روی خروجی HTML).
///
/// این‌ها جلوی برگشتنِ همان باگ قدیمی را می‌گیرند: تصمیم «داخل اپ / در مرورگر» باید فقط از
/// <c>AppClient.IsAppRequest</c> (User-Agent پوسته) بیاید و هیچ‌جای دیگری — نه کوکی، نه حافظه‌ی
/// مرورگر — حالت صفحه را نگه ندارد.
/// </summary>
public class ModeSourceContractTests
{
    /// <summary>فایل‌هایی که به‌طور مشروع نام کوکی قدیمی را برای پاک‌کردن ذکر می‌کنند.</summary>
    private static readonly string[] AppModeCookieAllowedFiles =
    {
        "Helpers/AppClient.cs",          // توضیح تصمیم و اینکه کوکی نقشی ندارد
        "Program.cs",                    // حذف کوکی بازمانده از نسخه‌های قبل
        "wwwroot/js/app-mode.js"         // پاک‌کردن همان کوکی در مرورگر
    };

    [Fact]
    public void Every_page_that_writes_app_mode_takes_it_from_AppClient()
    {
        // فقط «نوشتن» صفت مهم است (data-app-mode=)؛ خواندنش با جاوااسکریپت مجاز است
        var writers = RepositoryLayout.WebFiles(".cshtml")
            .Where(path => File.ReadAllText(path).Contains("data-app-mode=", StringComparison.Ordinal))
            .ToList();

        Assert.True(writers.Count >= 4,
            $"فایل‌های نویسنده‌ی data-app-mode کمتر از انتظار پیدا شد ({writers.Count})؛ اگر مسیر جست‌وجو " +
            "تغییر کرده، این آزمون باید به‌روز شود: " + string.Join(", ", writers.Select(Relative)));

        foreach (var file in writers)
        {
            var content = File.ReadAllText(file);
            Assert.True(content.Contains("AppClient.IsAppRequest", StringComparison.Ordinal),
                $"{Relative(file)} صفت data-app-mode را می‌نویسد ولی مقدارش را از AppClient.IsAppRequest " +
                "نمی‌گیرد؛ حالت صفحه باید فقط از User-Agent پوسته تعیین شود.");
        }
    }

    [Fact]
    public void Client_side_mode_writer_is_only_the_shared_script()
    {
        var jsWriters = RepositoryLayout.WebFiles(".js")
            .Where(path => File.ReadAllText(path).Contains("data-app-mode", StringComparison.Ordinal))
            .Select(Relative)
            .ToList();

        Assert.Equal(new[] { "wwwroot/js/app-mode.js" }, jsWriters);
    }

    [Fact]
    public void No_product_code_stores_the_mode_in_a_cookie()
    {
        foreach (var file in RepositoryLayout.WebFiles(".cs", ".cshtml"))
        {
            var relative = Relative(file);
            if (AppModeCookieAllowedFiles.Contains(relative)) continue;

            // نام کوکی فقط به‌صورت رشته‌ی نقل‌قول‌شده نشانه‌ی استفاده است؛
            // واژه‌ی شبیه آن در متن توضیحات (مثل نام این آزمون‌ها) مشکلی ندارد.
            var content = File.ReadAllText(file);
            Assert.DoesNotContain("\"appmode\"", content, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("'appmode", content, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("appmode=", content, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void The_shared_script_never_reads_the_mode_from_storage()
    {
        var script = File.ReadAllText(Path.Combine(RepositoryLayout.WebProjectDir, "wwwroot", "js", "app-mode.js"));

        Assert.DoesNotContain("localStorage", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sessionStorage", script, StringComparison.OrdinalIgnoreCase);

        // تنها نوشتنِ مجاز روی کوکی، پاک‌کردن کوکی بازمانده‌ی نسخه‌های قبلی است
        var cookieWrites = script
            .Split('\n')
            .Where(line => line.Contains("document.cookie", StringComparison.Ordinal))
            .Where(line => line.Contains('=') && !line.Contains("indexOf"))
            .ToList();

        Assert.All(cookieWrites, line => Assert.Contains("max-age=0", line));
    }

    [Fact]
    public void Separation_contract_markers_exist_in_the_views()
    {
        var webScoped = 0;
        var appScoped = 0;
        foreach (var file in RepositoryLayout.WebFiles(".cshtml"))
        {
            var content = File.ReadAllText(file);
            webScoped += CountOccurrences(content, "data-ui-scope=\"web\"");
            appScoped += CountOccurrences(content, "data-ui-scope=\"app\"");
        }

        Assert.True(webScoped >= 4,
            $"نشانه‌های وب‌محور (data-ui-scope=\"web\") کمتر از انتظار است ({webScoped}).");
        Assert.True(appScoped >= 2,
            $"نشانه‌های اپ‌محور (data-ui-scope=\"app\") کمتر از انتظار است ({appScoped}).");
    }

    [Fact]
    public void Automated_tests_environment_is_fenced_in_startup()
    {
        // حصار محیط Testing باید سر جایش باشد، وگرنه اجرای آزمون می‌تواند پیامک واقعی بفرستد
        var program = File.ReadAllText(Path.Combine(RepositoryLayout.WebProjectDir, "Program.cs"));

        Assert.Contains("IsEnvironment(\"Testing\")", program, StringComparison.Ordinal);
        Assert.Contains("if (!isAutomatedTest)", program, StringComparison.Ordinal);
        Assert.Contains("AddHostedService<SmsProcessor>()", program, StringComparison.Ordinal);
        Assert.Contains("if (!isAutomatedTest)\n    builder.Services.AddHostedService<SmsProcessor>();",
            program.Replace("\r\n", "\n"), StringComparison.Ordinal);
    }

    private static int CountOccurrences(string content, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = content.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }

    private static string Relative(string absolutePath)
        => Path.GetRelativePath(RepositoryLayout.WebProjectDir, absolutePath).Replace('\\', '/');
}
