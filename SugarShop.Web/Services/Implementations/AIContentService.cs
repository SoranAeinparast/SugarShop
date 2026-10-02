using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;
using SugarShop.Domain.Entities;
using SugarShop.Infrastructure.Persistence.Sales;
using SugarShop.Web.Services.Interfaces;
using System;
using System.ClientModel;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;

namespace SugarShop.Web.Services.Implementations
{
    public class AIContentService : IAIContentService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly SugarShopSalesDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AIContentService> _logger;

        private const string AGNES_API_URL = "https://apihub.agnes-ai.com/v1/images/generations";
        private const string POLLINATIONS_IMAGE_URL = "https://image.pollinations.ai/prompt/";
        private const string GEMINI_API_URL = "https://generativelanguage.googleapis.com/v1beta/models/";
        private const string OPENROUTER_API_URL = "https://openrouter.ai/api/v1";
        private const string OPENAI_API_URL = "https://api.openai.com/v1";
        private const string DEFAULT_GEMINI_MODEL = "gemini-2.0-flash";
        private const string DEFAULT_OPENROUTER_MODEL = "deepseek/deepseek-chat";
        private const string DEFAULT_OPENAI_MODEL = "gpt-4o-mini";

        public AIContentService(
            IHttpClientFactory httpClientFactory,
            SugarShopSalesDbContext context,
            IConfiguration configuration,
            ILogger<AIContentService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _context = context;
            _configuration = configuration;
            _logger = logger;
        }

        // ------------------------------------------------------------------
        // پیکربندی
        // ------------------------------------------------------------------

        /// <summary>کلید متن: اولویت با تنظیمات ذخیره‌شده در دیتابیس (پنل مدیریت)، سپس appsettings</summary>
        private async Task<string?> GetTextApiKeyAsync()
        {
            try
            {
                var dbKey = (await _context.AIContentSettings.FirstOrDefaultAsync())?.OpenAIApiKey;
                if (!string.IsNullOrWhiteSpace(dbKey)) return dbKey.Trim();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "خطا در خواندن تنظیمات AI از دیتابیس");
            }

            var configKeys = new[]
            {
                _configuration["OpenRouter:ApiKey"],
                _configuration["Gemini:ApiKey"]
            };
            foreach (var key in configKeys)
            {
                if (!string.IsNullOrWhiteSpace(key)) return key.Trim();
            }
            return null;
        }

        private async Task<string?> GetConfiguredModelNameAsync()
        {
            try
            {
                var model = (await _context.AIContentSettings.FirstOrDefaultAsync())?.ModelName;
                if (!string.IsNullOrWhiteSpace(model)) return model.Trim();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "خطا در خواندن مدل AI از دیتابیس");
            }
            return null;
        }

        private string? GetAgnesApiKey() => _configuration["AgnesAI:ApiKey"];

        public async Task<bool> IsTextAiConfiguredAsync()
        {
            var key = await GetTextApiKeyAsync();
            return !string.IsNullOrWhiteSpace(key);
        }

        /// <summary>تشخیص سرویس‌دهنده بر اساس پیشوند کلید (AIza…/AQ.… → Gemini، sk-or-… → OpenRouter، sk-… → OpenAI)</summary>
        private (string Provider, string Model, string Endpoint) DetectProvider(string apiKey, string? configuredModel)
        {
            // قالب جدید کلیدهای Google AI Studio با AQ. شروع می‌شود (به‌جای AIza قدیمی)
            if (apiKey.StartsWith("AIza", StringComparison.OrdinalIgnoreCase) ||
                apiKey.StartsWith("AQ.", StringComparison.OrdinalIgnoreCase))
            {
                return ("Gemini",
                    IsGeminiModel(configuredModel) ? configuredModel! : DEFAULT_GEMINI_MODEL,
                    GEMINI_API_URL);
            }
            if (apiKey.StartsWith("sk-or-", StringComparison.OrdinalIgnoreCase))
                return ("OpenRouter", configuredModel ?? DEFAULT_OPENROUTER_MODEL, OPENROUTER_API_URL);
            if (apiKey.StartsWith("sk-", StringComparison.OrdinalIgnoreCase))
                return ("OpenAI",
                    IsOpenAiModel(configuredModel) ? configuredModel! : DEFAULT_OPENAI_MODEL,
                    OPENAI_API_URL);

            // پیشوند ناشناخته: به‌جای هدایت بی‌صدا به OpenRouter، با پیام واضح خطا بدهیم
            throw new InvalidOperationException(
                $"پیشوند کلید شناسایی نشد («{GetKeyPrefix(apiKey)}…»). کلیدهای معتبر: Gemini (AIza… یا AQ.…)، OpenRouter (sk-or-…) یا OpenAI (sk-…).");
        }

        private static bool IsGeminiModel(string? model)
        {
            return !string.IsNullOrWhiteSpace(model) &&
                   (model.StartsWith("gemini", StringComparison.OrdinalIgnoreCase) ||
                    model.StartsWith("learnlm", StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsOpenAiModel(string? model)
        {
            return !string.IsNullOrWhiteSpace(model) &&
                   (model.StartsWith("gpt-", StringComparison.OrdinalIgnoreCase) ||
                    model.StartsWith("o1", StringComparison.OrdinalIgnoreCase) ||
                    model.StartsWith("o3", StringComparison.OrdinalIgnoreCase) ||
                    model.StartsWith("o4", StringComparison.OrdinalIgnoreCase) ||
                    model.StartsWith("chatgpt-", StringComparison.OrdinalIgnoreCase));
        }

        private static string GetKeyPrefix(string apiKey)
        {
            if (string.IsNullOrEmpty(apiKey)) return "";
            return apiKey.Length > 6 ? apiKey.Substring(0, 6) : apiKey;
        }

        // ------------------------------------------------------------------
        // متن
        // ------------------------------------------------------------------

        public async Task<string> RewriteContentAsync(string originalTitle, string originalContent)
        {
            var key = await GetTextApiKeyAsync();
            if (string.IsNullOrWhiteSpace(key))
            {
                _logger.LogInformation("هیچ کلید AI پیکربندی نشده است؛ پیش‌نویس ساختاریافته تولید می‌شود. عنوان: {Title}", originalTitle);
                return BuildStructuredArticleHtml(originalTitle, originalContent);
            }

            // اگر کلید شناسایی‌نشده باشد (مثلاً غلط یا از سرویس دیگری)، به جای کرش، پیش‌نویس ساختاریافته تولید و علت را لاگ می‌کنیم
            (string Provider, string Model, string Endpoint) provider;
            try
            {
                provider = DetectProvider(key, await GetConfiguredModelNameAsync());
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "کلید AI نامعتبر/ناشناخته؛ استفاده از پیش‌نویس ساختاریافته. عنوان: {Title}", originalTitle);
                return BuildStructuredArticleHtml(originalTitle, originalContent);
            }
            var prompt = BuildRewritePrompt(originalTitle, originalContent);

            try
            {
                var result = provider.Provider switch
                {
                    "Gemini" => await CallGeminiAsync(key, provider.Model, prompt),
                    _ => await CallOpenAiCompatibleAsync(key, provider.Provider, provider.Model, provider.Endpoint, prompt)
                };
                return CleanupMarkdown(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در بازنویسی محتوا با {Provider} برای عنوان {Title}", provider.Provider, originalTitle);
                throw;
            }
        }

        private string BuildRewritePrompt(string title, string content)
        {
            return $@"شما یک نویسنده حرفه‌ای محتوای آموزشی برای وب‌سایت شیرینی‌پزی هستید.
لطفاً مقاله زیر را به فارسی روان، ساده و جذاب بازنویسی کنید.

**دستورالعمل‌ها:**
- متن جدید باید کاملاً بازنویسی شود، نه ترجمهٔ تحت‌اللفظی.
- ساختار را حفظ کنید: مقدمه، مواد لازم، دستورالعمل مرحله‌به‌مرحله، نکات.
- از اصطلاحات رایج آشپزی فارسی استفاده کنید.
- خروجی را فقط با تگ‌های HTML (h2, ul, li, ol, p, strong, em) قالب‌بندی کنید و بدون هیچ توضیح اضافه.
- در انتها یک پاراگراف جمع‌بندی اضافه کنید.

**عنوان اصلی:** {title}
**متن اصلی:** {content}";
        }

        private async Task<string> CallGeminiAsync(string apiKey, string model, string prompt)
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("x-goog-api-key", apiKey);
            client.Timeout = TimeSpan.FromSeconds(120);

            var payload = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[] { new { text = prompt } }
                    }
                },
                generationConfig = new
                {
                    temperature = 0.7,
                    maxOutputTokens = 4000
                }
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await client.PostAsync($"{GEMINI_API_URL}{model}:generateContent", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                var message = ExtractErrorMessage(responseContent);
                // وقتی گوگل در سطح شبکه/منطقه دسترسی را مسدود کرده، پاسخ HTML است نه JSON
                if (string.IsNullOrWhiteSpace(message) &&
                    !string.IsNullOrWhiteSpace(responseContent) &&
                    responseContent.TrimStart().StartsWith("<", StringComparison.Ordinal))
                {
                    message = "سرور Google درخواست را در سطح شبکه مسدود کرد (احتمالاً IP/منطقه شما به سرویس Gemini دسترسی ندارد). از کلید OpenRouter یا اتصال با VPN/پروکسی استفاده کنید.";
                }
                message ??= $"کد خطا {(int)response.StatusCode}";
                throw new InvalidOperationException($"خطای Gemini ({response.StatusCode}): {message}");
            }

            using var doc = JsonDocument.Parse(responseContent);
            if (doc.RootElement.TryGetProperty("candidates", out var candidates) &&
                candidates.ValueKind == JsonValueKind.Array &&
                candidates.GetArrayLength() > 0 &&
                candidates[0].TryGetProperty("content", out var contentObj) &&
                contentObj.TryGetProperty("parts", out var parts) &&
                parts.ValueKind == JsonValueKind.Array &&
                parts.GetArrayLength() > 0 &&
                parts[0].TryGetProperty("text", out var text))
            {
                return text.GetString() ?? "";
            }

            var blocked = ExtractErrorMessage(responseContent);
            throw new InvalidOperationException($"پاسخی از Gemini دریافت نشد. {blocked}");
        }

        private async Task<string> CallOpenAiCompatibleAsync(string apiKey, string provider, string model, string endpoint, string prompt)
        {
            var options = new OpenAIClientOptions { Endpoint = new Uri(endpoint) };
            var client = new OpenAIClient(new ApiKeyCredential(apiKey), options);
            var chatClient = client.GetChatClient(model);

            var response = await chatClient.CompleteChatAsync(
                new ChatMessage[]
                {
                    new SystemChatMessage("شما یک نویسنده حرفه‌ای محتوای آموزشی شیرینی‌پزی هستید و همیشه به فارسی پاسخ می‌دهید."),
                    new UserChatMessage(prompt)
                },
                new ChatCompletionOptions { Temperature = 0.7f, MaxOutputTokenCount = 4000 });

            return response.Value.Content[0].Text ?? "";
        }

        private static string CleanupMarkdown(string result)
        {
            result = result?.Trim() ?? "";
            if (result.StartsWith("```html", StringComparison.OrdinalIgnoreCase))
                result = result.Substring(7);
            else if (result.StartsWith("```"))
                result = result.Substring(3);
            if (result.EndsWith("```"))
                result = result.Substring(0, result.Length - 3);
            return result.Trim();
        }

        /// <summary>پیش‌نویس ساختاریافته و حرفه‌ای HTML وقتی کلید AI تنظیم نشده است (همیشه کار می‌کند)</summary>
        private string BuildStructuredArticleHtml(string title, string content)
        {
            var safeTitle = HttpUtility.HtmlEncode(string.IsNullOrWhiteSpace(title) ? "مقاله آموزشی شیرینی‌پزی" : title.Trim());
            var lines = (content ?? "")
                .Replace("\r\n", "\n")
                .Split('\n');

            var ingredients = new System.Collections.Generic.List<string>();
            var steps = new System.Collections.Generic.List<string>();
            var paragraphs = new System.Collections.Generic.List<string>();
            var current = new StringBuilder();

            void FlushParagraph()
            {
                if (current.Length == 0) return;
                var text = current.ToString().Trim();
                if (text.Length > 0) paragraphs.Add(text);
                current.Clear();
            }

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line))
                {
                    FlushParagraph();
                    continue;
                }

                var normalized = line.TrimStart('-', '•', '*', '‣', '▪', '1', '2', '3', '4', '5', '6', '7', '8', '9', '0', '.', ')', ' ').Trim();

                if (line.Length <= 90 && IsIngredientLine(line))
                {
                    FlushParagraph();
                    ingredients.Add(HttpUtility.HtmlEncode(line.TrimStart('-', '•', '*', '‣', '▪').Trim()));
                }
                else if (line.Length <= 160 && (line.EndsWith(".") || line.EndsWith("؛") || line.EndsWith(":") || line.Length <= 60) && LooksLikeStep(line))
                {
                    FlushParagraph();
                    steps.Add(HttpUtility.HtmlEncode(line));
                }
                else
                {
                    current.AppendLine(line);
                }
            }
            FlushParagraph();

            var html = new StringBuilder();
            html.AppendLine("<article>");

            html.AppendLine("<section>");
            html.AppendLine("<h2>✨ مقدمه</h2>");
            if (paragraphs.Count > 0)
            {
                foreach (var p in paragraphs.Take(3))
                    html.AppendLine($"<p>{p}</p>");
            }
            else
            {
                html.AppendLine($"<p>در این آموزش، طرز تهیه «{safeTitle}» را به صورت کامل و مرحله‌به‌مرحله فرا خواهید گرفت. این دستور پخت برای شیرینی‌پزهای خانگی و حرفه‌ای به یک اندازه کاربردی است و نتیجه‌ای خوش‌طعم و باکیفیت به همراه دارد.</p>");
            }
            html.AppendLine("</section>");

            if (ingredients.Count > 0)
            {
                html.AppendLine("<section>");
                html.AppendLine("<h2>🧺 مواد لازم</h2>");
                html.AppendLine("<ul>");
                foreach (var item in ingredients)
                    html.AppendLine($"<li>{item}</li>");
                html.AppendLine("</ul>");
                html.AppendLine("</section>");
            }

            if (steps.Count > 0)
            {
                html.AppendLine("<section>");
                html.AppendLine("<h2>👩‍🍳 طرز تهیه</h2>");
                html.AppendLine("<ol>");
                foreach (var step in steps)
                    html.AppendLine($"<li>{step}</li>");
                html.AppendLine("</ol>");
                html.AppendLine("</section>");
            }
            else if (paragraphs.Count > 3)
            {
                html.AppendLine("<section>");
                html.AppendLine("<h2>👩‍🍳 مراحل تهیه</h2>");
                var stepIndex = 1;
                foreach (var p in paragraphs.Skip(3))
                    html.AppendLine($"<p><strong>مرحله {stepIndex++}:</strong> {p}</p>");
                html.AppendLine("</section>");
            }

            html.AppendLine("<section>");
            html.AppendLine("<h2>💡 نکات مهم</h2>");
            html.AppendLine("<ul>");
            html.AppendLine("<li>همه مواد را قبل از شروع کار در دمای اتاق آماده کنید تا بافت بهتری بگیرید.</li>");
            html.AppendLine("<li>فر را از قبل گرم کنید و دمای دقیق را رعایت نمایید.</li>");
            html.AppendLine("<li>پس از آماده شدن، اجازه دهید دسر کمی خنک شود تا برش تمیزتری داشته باشد.</li>");
            html.AppendLine("</ul>");
            html.AppendLine("</section>");

            html.AppendLine("<p><strong>جمع‌بندی:</strong> با رعایت همین نکات ساده می‌توانید این دسر خوشمزه را در خانه با کیفیت قنادی تهیه کنید و از نتیجه آن لذت ببرید. نوش جان!</p>");
            html.AppendLine("</article>");
            return html.ToString();
        }

        private static bool IsIngredientLine(string line)
        {
            var lower = line.ToLowerInvariant();
            string[] signals = { "گرم", "پیمانه", "قاشق", "عدد", "کیلو", "لیوان", "مقدار", "چای", "رب", "آرد", "شکر", "روغن", "کره", "تخم‌مرغ", "وانیل", "بکینگ", "نمک", "پودر", "شیر", "خامه", "پنیر", "مربا", "عسل", "پسته", "بادام", "گردو", "کشمش", "نسکافه", "پودر قند", "ژلاتین", "آب", "سس", "ماست", "زعفران", "گلاب", "هل", "دارچین", "کاکائو", "شکلات", "خمیر", "بیسکویت" };
            foreach (var s in signals)
            {
                if (lower.Contains(s)) return true;
            }
            return line.Length <= 60 && (lower.Contains("عدد") || lower.Contains("گرم") || lower.Contains("پیمانه") || lower.Contains("قاشق"));
        }

        private static bool LooksLikeStep(string line)
        {
            var lower = line.ToLowerInvariant();
            string[] signals = { "ابتدا", "اول", "سپس", "بعد", "در مرحله", "در ادامه", "در نهایت", "در پایان", "مرحله", "آخر", "مخلوط", "اضافه", "بریز", "بزنید", "بگذارید", "بردارید", "قرار", "حرارت", "داخل فر", "روی حرارت", "خنک", "الک", "هم بزنید", "هم بزن", "قالب", "فر" };
            foreach (var s in signals)
            {
                if (lower.Contains(s)) return true;
            }
            return false;
        }

        // ------------------------------------------------------------------
        // تصویر
        // ------------------------------------------------------------------

        public async Task<string> GenerateFeaturedImageAsync(string title, string description)
        {
            try
            {
                var agnesKey = GetAgnesApiKey();
                if (!string.IsNullOrWhiteSpace(agnesKey))
                {
                    try
                    {
                        return await GenerateWithAgnesAsync(agnesKey, title, description);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "AgnesAI ناموفق بود؛ تلاش با سرویس رایگان تصویر");
                    }
                }

                return GenerateWithPollinations(title, description);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در تولید تصویر شاخص");
                return "https://via.placeholder.com/1024x1024/FFD700/000000?text=Recipe+Image";
            }
        }

        private async Task<string> GenerateWithAgnesAsync(string apiKey, string title, string description)
        {
            var prompt = $"A professional food photography image of {title}, {description}, 4k, highly detailed, beautiful lighting, delicious, mouth-watering";
            var payload = new
            {
                model = "Agnes-Image-2.1-Flash",
                prompt,
                n = 1,
                size = "1024x1024"
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
            client.Timeout = TimeSpan.FromSeconds(60);

            var response = await client.PostAsync(AGNES_API_URL, content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"AgnesAI: {(int)response.StatusCode} {responseContent}");

            using var doc = JsonDocument.Parse(responseContent);
            var imageUrl = doc.RootElement.GetProperty("data")[0].GetProperty("url").GetString();
            return string.IsNullOrWhiteSpace(imageUrl)
                ? "https://via.placeholder.com/1024x1024/FFD700/000000?text=Recipe+Image"
                : imageUrl;
        }

        /// <summary>تولید تصویر رایگان با Pollinations (بدون نیاز به کلید)</summary>
        private string GenerateWithPollinations(string title, string description)
        {
            var cleanTitle = (title ?? "").Trim();
            if (cleanTitle.Length > 60) cleanTitle = cleanTitle.Substring(0, 60);
            var cleanDescription = (description ?? "").Trim();
            if (cleanDescription.Length > 80) cleanDescription = cleanDescription.Substring(0, 80);

            var prompt = $"Professional food photography of {cleanTitle}. {cleanDescription}. Beautiful soft lighting, highly detailed, mouth-watering, appetizing, 4k";
            var encoded = Uri.EscapeDataString(prompt);
            var seed = new Random().Next(1, 999999);

            return $"{POLLINATIONS_IMAGE_URL}{encoded}?width=1024&height=1024&model=flux&seed={seed}&nologo=true";
        }

        // ------------------------------------------------------------------
        // تست اتصال
        // ------------------------------------------------------------------

        public async Task<AIConnectionTestResult> TestConnectionAsync()
        {
            var key = await GetTextApiKeyAsync();
            if (string.IsNullOrWhiteSpace(key))
            {
                return new AIConnectionTestResult
                {
                    Success = false,
                    Provider = "بدون کلید",
                    Message = "هیچ کلیدی پیکربندی نشده است. سیستم در «حالت پیش‌نویس ساختاریافته» کار می‌کند. برای فعال‌سازی تولید متن هوشمند، کلید رایگان Gemini یا OpenRouter را از پنل تنظیمات وارد کنید.",
                    LatencyMs = 0
                };
            }

            (string Provider, string Model, string Endpoint) provider;
            try
            {
                provider = DetectProvider(key, await GetConfiguredModelNameAsync());
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "کلید AI با پیشوند ناشناخته در تست اتصال");
                return new AIConnectionTestResult
                {
                    Success = false,
                    Provider = "نامشخص",
                    Message = $"اتصال ناموفق ❌ — {ex.Message}",
                    LatencyMs = 0
                };
            }

            var sw = Stopwatch.StartNew();
            try
            {
                var text = provider.Provider switch
                {
                    "Gemini" => await CallGeminiAsync(key, provider.Model, "فقط یک جمله کوتاه به فارسی بنویس: سلام!"),
                    _ => await CallOpenAiCompatibleAsync(key, provider.Provider, provider.Model, provider.Endpoint, "فقط یک جمله کوتاه به فارسی بنویس: سلام!")
                };
                sw.Stop();
                var snippet = text.Trim();
                if (snippet.Length > 80) snippet = snippet.Substring(0, 80) + "…";
                return new AIConnectionTestResult
                {
                    Success = true,
                    Provider = provider.Provider,
                    Message = $"اتصال موفق ✅ — پاسخ سرویس: «{snippet}»",
                    LatencyMs = sw.ElapsedMilliseconds
                };
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogWarning(ex, "تست اتصال ناموفق برای {Provider}", provider.Provider);
                return new AIConnectionTestResult
                {
                    Success = false,
                    Provider = provider.Provider,
                    Message = $"اتصال ناموفق ❌ — {ex.Message}",
                    LatencyMs = sw.ElapsedMilliseconds
                };
            }
        }

        private static string? ExtractErrorMessage(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("error", out var error))
                {
                    if (error.TryGetProperty("message", out var msg))
                        return msg.GetString();
                    if (error.ValueKind == JsonValueKind.String)
                        return error.GetString();
                }
            }
            catch
            {
                // ignore
            }
            return null;
        }
    }
}