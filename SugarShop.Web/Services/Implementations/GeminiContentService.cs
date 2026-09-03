using System;
using System.Threading.Tasks;
using Google.GenAI;
using Microsoft.Extensions.Logging;
using SugarShop.Web.Services.Interfaces;

namespace SugarShop.Web.Services.Implementations
{
    public class GeminiContentService : IAIContentService
    {
        private readonly Client _client;
        private readonly ILogger<GeminiContentService> _logger;

        public GeminiContentService(IConfiguration configuration, ILogger<GeminiContentService> logger)
        {
            var apiKey = configuration["Gemini:ApiKey"]
                         ?? throw new InvalidOperationException("Gemini API Key not found in appsettings.json");
            _client = new Client(apiKey: apiKey);
            _logger = logger;
        }

        public async Task<string> RewriteContentAsync(string originalTitle, string originalContent)
        {
            var prompt = $@"
            شما یک نویسنده حرفه‌ای محتوای آموزشی برای وب‌سایت شیرینی‌پزی هستید.
            لطفاً مقاله زیر را به فارسی روان، ساده و جذاب بازنویسی کنید.

            **دستورالعمل‌ها:**
            - متن جدید باید کاملاً بازنویسی شود، نه ترجمهٔ تحت‌اللفظی.
            - ساختار را حفظ کنید: مقدمه، مواد لازم، دستورالعمل مرحله‌به‌مرحله، نکات.
            - از اصطلاحات رایج آشپزی فارسی استفاده کنید.
            - خروجی را با تگ‌های HTML (h2, ul, li, p, strong, em) قالب‌بندی کنید.

            **عنوان اصلی:** {originalTitle}
            **متن اصلی:** {originalContent}
            ";

            try
            {
                var response = await _client.Models.GenerateContentAsync(
                    model: "gemini-1.5-flash",
                    contents: prompt
                );

                return response.Candidates[0].Content.Parts[0].Text;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while rewriting content with Gemini");
                throw;
            }
        }

        public Task<string> GenerateFeaturedImageAsync(string title, string description)
        {
            _logger.LogWarning("Gemini does not support image generation yet.");
            return Task.FromResult("https://via.placeholder.com/1024x1024/FFD700/000000?text=Recipe+Image");
        }
    }
}