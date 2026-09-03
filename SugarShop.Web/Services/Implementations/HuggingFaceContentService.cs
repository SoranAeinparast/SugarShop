using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SugarShop.Web.Services.Interfaces;

namespace SugarShop.Web.Services.Implementations
{
    public class HuggingFaceContentService : IAIContentService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly ILogger<HuggingFaceContentService> _logger;
        private const string API_URL = "https://api-inference.huggingface.co/models/";
        private const string DEFAULT_MODEL = "google/mt5-small";

        public HuggingFaceContentService(IConfiguration configuration, ILogger<HuggingFaceContentService> logger)
        {
            _apiKey = configuration["HuggingFace:ApiKey"]
                      ?? throw new InvalidOperationException("Hugging Face API Key not found.");
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
            _logger = logger;
        }

        public async Task<string> RewriteContentAsync(string originalTitle, string originalContent)
        {
            var inputText = $"بازنویسی کن: عنوان: {originalTitle}. متن: {originalContent}";
            if (inputText.Length > 1000)
                inputText = inputText.Substring(0, 1000);

            var payload = new
            {
                inputs = inputText,
                parameters = new
                {
                    max_length = 500,
                    temperature = 0.7,
                    do_sample = true
                }
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                _logger.LogInformation($"Sending request to Hugging Face API with model: {DEFAULT_MODEL}");
                var response = await _httpClient.PostAsync($"{API_URL}{DEFAULT_MODEL}", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"Hugging Face API Error: {responseContent}");
                    return $"خطا در تولید محتوا: {responseContent}";
                }
                using var doc = JsonDocument.Parse(responseContent);
                var generatedText = doc.RootElement[0].GetProperty("generated_text").GetString();

                return generatedText ?? "متنی تولید نشد.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling Hugging Face API");
                return $"خطا: {ex.Message}";
            }
        }

        public Task<string> GenerateFeaturedImageAsync(string title, string description)
        {
            _logger.LogWarning("Image generation not implemented for Hugging Face yet.");
            return Task.FromResult("https://via.placeholder.com/1024x1024/FFD700/000000?text=Recipe+Image");
        }
    }
}