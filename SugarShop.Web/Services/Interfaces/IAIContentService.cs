using System.Threading.Tasks;

namespace SugarShop.Web.Services.Interfaces
{
    public interface IAIContentService
    {
        Task<string> RewriteContentAsync(string originalTitle, string originalContent);
        Task<string> GenerateFeaturedImageAsync(string title, string description);
    }
}