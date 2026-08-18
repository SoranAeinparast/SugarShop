using System.Threading.Tasks;

namespace SugarShop.Web.Services.Interfaces
{
    public interface IWebScraperService
    {
        Task<string> FetchHtmlAsync(string url);
    }
}