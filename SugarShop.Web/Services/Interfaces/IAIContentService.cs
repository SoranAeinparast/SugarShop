using System.Threading.Tasks;

namespace SugarShop.Web.Services.Interfaces
{
    public class AIConnectionTestResult
    {
        public bool Success { get; set; }
        public string Provider { get; set; } = "";
        public string Message { get; set; } = "";
        public long LatencyMs { get; set; }
    }

    public interface IAIContentService
    {
        /// <summary>بازنویسی/تولید محتوای آموزشی با هوش مصنوعی (یا پیش‌نویس ساختاریافته در صورت نبود کلید)</summary>
        Task<string> RewriteContentAsync(string originalTitle, string originalContent);

        /// <summary>تولید تصویر شاخص برای مقاله (AgnesAI در صورت وجود کلید، در غیر این صورت Pollinations رایگان)</summary>
        Task<string> GenerateFeaturedImageAsync(string title, string description);

        /// <summary>آیا کلید متنی (Gemini/OpenRouter/OpenAI) پیکربندی شده است؟</summary>
        Task<bool> IsTextAiConfiguredAsync();

        /// <summary>تست اتصال به سرویس متن انتخابی و بازگرداندن وضعیت + تأخیر</summary>
        Task<AIConnectionTestResult> TestConnectionAsync();
    }
}