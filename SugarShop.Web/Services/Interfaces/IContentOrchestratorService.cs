using System.Collections.Generic;
using System.Threading.Tasks;

namespace SugarShop.Web.Services.Interfaces
{
    public interface IContentOrchestratorService
    {
        Task RunContentPipelineAsync(List<string> sourceUrls);
    }
}