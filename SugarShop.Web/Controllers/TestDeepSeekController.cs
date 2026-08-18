using Microsoft.AspNetCore.Mvc;
using SugarShop.Web.Services.Interfaces;
using System.Threading.Tasks;

namespace SugarShop.Web.Controllers
{
    public class TestDeepSeekController : Controller
    {
        private readonly IAIContentService _aiService;

        public TestDeepSeekController(IAIContentService aiService)
        {
            _aiService = aiService;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var result = await _aiService.RewriteContentAsync(
                    "کیک اسفنجی ساده",
                    "مواد: ۳ عدد تخم‌مرغ، ۱ لیوان شکر، ۱ لیوان آرد، ۱ قاشق چای‌خوری بیکینگ پودر. روش: تخم‌مرغ و شکر را بزنید تا کرم‌رنگ شود، آرد و بیکینگ پودر را اضافه کنید، مخلوط کنید، در قالب بریزید و در فر ۱۸۰ درجه به مدت ۳۰ دقیقه بپزید."
                );
                return Content(result);
            }
            catch (System.Exception ex)
            {
                return Content($"❌ خطا: {ex.Message}");
            }
        }
    }
}