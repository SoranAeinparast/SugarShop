using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SugarShop.Web.Controllers
{
    [Authorize(Roles = "Admin,OrderManager,Owner")]
    public class SalesManagementController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}