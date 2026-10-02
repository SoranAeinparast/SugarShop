using HtmlAgilityPack;

namespace SugarShop.Web.Tests.Infrastructure;

/// <summary>ابزارهای کوچک خواندن HTML پاسخ‌ها — بدون هیچ وابستگی سنگین (HtmlAgilityPack).</summary>
public static class HtmlProbe
{
    public static HtmlDocument Parse(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        return doc;
    }

    /// <summary>مقدار <c>data-app-mode</c> روی عنصر <c>&lt;html&gt;</c> (null اگر نبود).</summary>
    public static string? AppMode(HtmlDocument doc)
        => doc.DocumentNode.SelectSingleNode("//html")?.Attributes["data-app-mode"]?.Value;

    /// <summary>کلاس‌های روی عنصر <c>&lt;html&gt;</c>.</summary>
    public static string[] RootClasses(HtmlDocument doc)
        => (doc.DocumentNode.SelectSingleNode("//html")?.GetAttributeValue("class", string.Empty) ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>همه‌ی عنصرهایی که قرارداد «فقط اپ / فقط وب» را دارند.</summary>
    public static IReadOnlyList<HtmlNode> WithScope(HtmlDocument doc, string scope)
        => doc.DocumentNode.SelectNodes($"//*[@data-ui-scope='{scope}']")?.ToList()
           ?? new List<HtmlNode>();

    /// <summary>آیا عنصری با این XPath وجود دارد؟</summary>
    public static bool Exists(HtmlDocument doc, string xpath)
        => doc.DocumentNode.SelectSingleNode(xpath) != null;

    /// <summary>توصیف کوتاه عنصرها برای پیام شکست آزمون.</summary>
    public static string Describe(IEnumerable<HtmlNode> nodes)
        => string.Join(" ، ", nodes.Take(4).Select(n =>
            $"<{n.Name} class=\"{n.GetAttributeValue("class", "-")}\">"));
}
