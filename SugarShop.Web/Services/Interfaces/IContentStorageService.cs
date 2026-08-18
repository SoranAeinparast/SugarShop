using System.Threading.Tasks;
using SugarShop.Domain.Entities;

namespace SugarShop.Web.Services.Interfaces
{
    public interface IContentStorageService
    {
        Task<int> SaveContentAsync(EducationalContent content);
    }
}