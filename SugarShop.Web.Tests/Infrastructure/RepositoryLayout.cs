namespace SugarShop.Web.Tests.Infrastructure;

/// <summary>مسیرهای مخزن برای آزمون‌هایی که خودِ فایل‌های منبع را بررسی می‌کنند.</summary>
public static class RepositoryLayout
{
    public static string Root { get; } = FindRoot();

    public static string WebProjectDir => Path.Combine(Root, "SugarShop.Web");

    /// <summary>فایل‌های منبع پروژه‌ی وب (بدون bin/obj و کتابخانه‌های wwwroot/lib).</summary>
    public static IReadOnlyList<string> WebFiles(params string[] extensions)
    {
        var wanted = extensions.Length == 0 ? new[] { ".cs", ".cshtml" } : extensions;
        return Directory.EnumerateFiles(WebProjectDir, "*.*", SearchOption.AllDirectories)
            .Where(path => wanted.Any(ext => path.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
            .Where(path => !IsGenerated(path))
            .ToList();
    }

    private static bool IsGenerated(string path)
    {
        var normalized = path.Replace('\\', '/');
        return normalized.Contains("/bin/")
            || normalized.Contains("/obj/")
            || normalized.Contains("/wwwroot/lib/")
            || normalized.Contains("/node_modules/");
    }

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "SugarShop.Web", "SugarShop.Web.csproj")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            "ریشه‌ی مخزن پیدا نشد؛ آزمون‌های بررسی فایل‌های منبع باید از داخل مخزن اجرا شوند.");
    }
}
