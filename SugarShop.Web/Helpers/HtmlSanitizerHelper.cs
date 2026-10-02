using System;
using System.Collections.Generic;
using System.Linq;
using HtmlAgilityPack;

namespace SugarShop.Web.Helpers
{
    /// <summary>
    /// پاکسازی HTML با لیست سفید تگ‌ها برای جلوگیری از XSS.
    /// از HtmlAgilityPack استفاده می‌کند که از قبل در پروژه موجود است (بدون وابستگی جدید).
    /// تگ‌های خطرناک (script, style, iframe و...) حذف و همه event handler ها و URI های غیرمجاز پاک می‌شوند.
    /// </summary>
    public static class HtmlSanitizerHelper
    {
        private static readonly HashSet<string> AllowedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "p", "br", "hr", "div", "span", "article", "section", "header", "footer",
            "h1", "h2", "h3", "h4", "h5", "h6",
            "strong", "b", "em", "i", "u", "s", "mark", "small",
            "blockquote", "cite", "code", "pre",
            "ul", "ol", "li", "dl", "dt", "dd",
            "a", "img", "figure", "figcaption",
            "table", "thead", "tbody", "tfoot", "tr", "th", "td"
        };

        private static readonly HashSet<string> AllowedUriSchemes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "http", "https", "mailto", "tel"
        };

        public static string Sanitize(string? html)
        {
            if (string.IsNullOrWhiteSpace(html))
                return string.Empty;

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // ابتدا همه گره‌ها را جمع می‌کنیم تا تغییرات هنگام پیمایش مشکلی ایجاد نکند
            var nodes = doc.DocumentNode.DescendantsAndSelf().ToList();

            foreach (var node in nodes)
            {
                if (node.NodeType == HtmlNodeType.Comment)
                {
                    node.Remove();
                    continue;
                }

                if (node.NodeType != HtmlNodeType.Element)
                    continue;

                if (!AllowedTags.Contains(node.Name))
                {
                    // تگ غیرمجاز حذف می‌شود ولی متن داخل آن حفظ می‌شود
                    Unwrap(node);
                    continue;
                }

                // حذف event handler ها و ویژگی‌های غیرمجاز
                var attributes = node.Attributes.ToList();
                foreach (var attr in attributes)
                {
                    string name = attr.Name.ToLowerInvariant();
                    bool allowed = name == "alt"
                        || name == "dir" // پشتیبانی از متن راست‌به‌چپ
                        || (name == "colspan" || name == "rowspan");

                    if (!allowed && name == "href" && node.Name == "a")
                        allowed = IsSafeUrl(attr.Value);
                    else if (!allowed && name == "src" && node.Name == "img")
                        allowed = IsSafeImageUrl(attr.Value);

                    if (!allowed)
                        attr.Remove();
                }

                // تصویری که src مجاز ندارد بی‌فایده است؛ حذفش می‌کنیم تا آیکون خراب نمایش داده نشود
                if (node.Name == "img" && node.GetAttributeValue("src", "") == "")
                    node.Remove();
            }

            return doc.DocumentNode.InnerHtml;
        }

        private static void Unwrap(HtmlNode node)
        {
            if (node.ParentNode == null)
                return;

            var children = node.ChildNodes.ToList();
            foreach (var child in children)
                node.ParentNode.InsertBefore(child, node);

            node.Remove();
        }

        private static bool IsSafeUrl(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            var url = value.Trim();

            // لینک‌های نسبی یا لنگر داخل صفحه
            if (url.StartsWith("/") || url.StartsWith("#"))
                return true;

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return false;

            return AllowedUriSchemes.Contains(uri.Scheme);
        }

        private static bool IsSafeImageUrl(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            var url = value.Trim();

            // مسیر نسبی روی همین سایت
            if (url.StartsWith("/") || url.StartsWith("//"))
                return true;

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return false;

            return AllowedUriSchemes.Contains(uri.Scheme);
        }
    }
}
