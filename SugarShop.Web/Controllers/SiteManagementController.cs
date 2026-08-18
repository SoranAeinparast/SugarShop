using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SugarShop.Web.Controllers
{
    [Authorize(Roles = "Admin,Owner")]
    public class SiteManagementController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}