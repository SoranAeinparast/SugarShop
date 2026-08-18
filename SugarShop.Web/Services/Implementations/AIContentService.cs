using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;
using SugarShop.Web.Services.Interfaces;
using System;
using System.ClientModel;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SugarShop.Web.Services.Implementations
{
    public class AIContentService : IAIContentService
    {
        private readonly ChatClient _chatClient;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _agnesApiKey;
        private readonly ILogger<AIContentService> _logger;
        private const string AGNES_API_URL = "https://apihub.agnes-ai.com/v1/images/generations";

        public AIContentService(IConfiguration configuration, IHttpClientFactory httpClientFactory, ILogger<AIContentService> logger)
        {
            var apiKey = configuration["OpenRouter:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                logger.LogWarning("OpenRouter API Key not configured. AI content features will be disabled.");
                apiKey = "dummy-key";
            }

            _agnesApiKey = configuration["AgnesAI:ApiKey"] ?? "";
            _httpClientFactory = httpClientFactory;
            _logger = logger;

            var options = new OpenAIClientOptions
            {
                Endpoint = new Uri("https://openrouter.ai/api/v1")
            };

            var credential = new ApiKeyCredential(apiKey);
            var client = new OpenAIClient(credential, options);
            _chatClient = client.GetChatClient("deepseek/deepseek-chat");
        }

        public async Task<string> RewriteContentAsync(string originalTitle, string originalContent)
        {
            try
            {
                var prompt = $@"
            شما یک نویسنده حرفه‌ای محتوای آموزشی برای وب‌سایت شیرینی‌پزی هستید.
            لطفاً مقاله زیر را به فارسی روان، ساده و جذاب بازنویسی کنید.

            **دستورالعمل‌ها:**
            - متن جدید باید کاملاً بازنویسی شود، نه ترجمهٔ تحت‌اللفظی.
            - ساختار را حفظ کنید: مقدمه، مواد لازم، دستورالعمل مرحله‌به‌مرحله، نکات.
            - از اصطلاحات رایج آشپزی فارسی استفاده کنید.
            - خروجی را با تگ‌های HTML (h2, ul, li, p, strong, em) قالب‌بندی کنید.
            - در انتها یک پاراگراف جمع‌بندی اضافه کنید.

            **عنوان اصلی:** {originalTitle}
            **متن اصلی:** {originalContent}
            ";

                var response = await _chatClient.CompleteChatAsync(prompt);
                var result = response.Value.Content[0].Text;

                // Clean up markdown code fences if present
                result = result.Trim();
                if (result.StartsWith("```html"))
                    result = result.Substring(7);
                else if (result.StartsWith("```"))
                    result = result.Substring(3);
                if (result.EndsWith("```"))
                    result = result.Substring(0, result.Length - 3);
                result = result.Trim();

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rewriting content with AI for title: {Title}", originalTitle);
                throw;
            }
        }

        public async Task<string> GenerateFeaturedImageAsync(string title, string description)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_agnesApiKey))
                {
                    _logger.LogWarning("AgnesAI API key not configured, using placeholder image");
                    return "https://via.placeholder.com/1024x1024/FFD700/000000?text=Recipe+Image";
                }

                var prompt = $"A professional food photography image of {title}, {description}, 4k, highly detailed, beautiful lighting, delicious, mouth-watering";
                var payload = new
                {
                    model = "Agnes-Image-2.1-Flash",
                    prompt = prompt,
                    n = 1,
                    size = "1024x1024"
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_agnesApiKey}");
                client.Timeout = TimeSpan.FromSeconds(30);

                var response = await client.PostAsync(AGNES_API_URL, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("AgnesAI returned {StatusCode}: {Body}", response.StatusCode, responseContent);
                    return "https://via.placeholder.com/1024x1024/FFD700/000000?text=Recipe+Image";
                }

                using var doc = JsonDocument.Parse(responseContent);
                var imageUrl = doc.RootElement.GetProperty("data")[0].GetProperty("url").GetString();

                return imageUrl ?? "https://via.placeholder.com/1024x1024/FFD700/000000?text=Recipe+Image";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating featured image");
                return "https://via.placeholder.com/1024x1024/FFD700/000000?text=Recipe+Image";
            }
        }
    }
}
