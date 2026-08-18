using System.Collections.Generic;

namespace SugarShop.Web.Services.Interfaces
{
    public interface IContentParserService
    {
        (string Title, string Content, List<string> ImageUrls) ParseArticle(string html);
    }
}