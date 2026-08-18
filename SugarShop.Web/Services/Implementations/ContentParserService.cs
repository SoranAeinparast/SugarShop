using System.Collections.Generic;
using System.Linq;
using HtmlAgilityPack;
using SugarShop.Web.Services.Interfaces;

namespace SugarShop.Web.Services.Implementations
{
    public class ContentParserService : IContentParserService
    {
        public (string Title, string Content, List<string> ImageUrls) ParseArticle(string html)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var titleNode = doc.DocumentNode.SelectSingleNode("//h1[contains(@class, 'entry-title')]")
                            ?? doc.DocumentNode.SelectSingleNode("//h1[contains(@class, 'title')]")
                            ?? doc.DocumentNode.SelectSingleNode("//h1");

            var contentNode = doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'entry-content')]")
                              ?? doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'post-content')]")
                              ?? doc.DocumentNode.SelectSingleNode("//article");

            var title = titleNode?.InnerText.Trim() ?? "بدون عنوان";
            var content = contentNode?.InnerHtml ?? string.Empty;
            var imageUrls = new List<string>();
            if (contentNode != null)
            {
                var imgNodes = contentNode.SelectNodes(".//img");
                if (imgNodes != null)
                {
                    foreach (var img in imgNodes)
                    {
                        var src = img.GetAttributeValue("src", "");
                        if (!string.IsNullOrEmpty(src) &&
                            (src.StartsWith("http://") || src.StartsWith("https://")))
                        {
                            imageUrls.Add(src);
                        }
                    }
                }
            }

            return (title, content, imageUrls);
        }
    }
}