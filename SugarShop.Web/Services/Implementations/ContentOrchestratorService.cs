using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using SugarShop.Domain.Entities;
using SugarShop.Infrastructure.Persistence.Sales;
using SugarShop.Web.Services.Interfaces;

namespace SugarShop.Web.Services.Implementations
{
    public class ContentOrchestratorService : IContentOrchestratorService
    {
        private readonly IWebScraperService _scraper;
        private readonly IContentParserService _parser;
        private readonly IAIContentService _ai;
        private readonly IContentStorageService _storage;
        private readonly SugarShopSalesDbContext _context;
        private readonly ILogger<ContentOrchestratorService> _logger;

        public ContentOrchestratorService(
            IWebScraperService scraper,
            IContentParserService parser,
            IAIContentService ai,
            IContentStorageService storage,
            SugarShopSalesDbContext context,
            ILogger<ContentOrchestratorService> logger)
        {
            _scraper = scraper;
            _parser = parser;
            _ai = ai;
            _storage = storage;
            _context = context;
            _logger = logger;
        }

        public async Task RunContentPipelineAsync(List<string> sourceUrls)
        {
            foreach (var url in sourceUrls)
            {
                try
                {
                    _logger.LogInformation($"⏳ Processing: {url}");

                    var html = await _scraper.FetchHtmlAsync(url);
                    var (title, content, imageUrls) = _parser.ParseArticle(html);

                    if (string.IsNullOrWhiteSpace(content))
                    {
                        _logger.LogWarning($"⚠️ No content extracted from {url}");
                        continue;
                    }
                    var normalizedUrl = NormalizeUrl(url);
                    var existing = await _context.EducationalContents
                        .FirstOrDefaultAsync(c =>
                            c.SourceUrl != null &&
                            c.SourceUrl.ToLower() == normalizedUrl.ToLower() &&
                            c.Title.ToLower() == title.ToLower());

                    if (existing != null)
                    {
                        _logger.LogInformation($"⏭️ '{title}' already exists from '{url}'. Skipping.");
                        continue;
                    }

                    var rewritten = await _ai.RewriteContentAsync(title, content);
                    var cleaned = rewritten;
                    if (cleaned.StartsWith("```html"))
                        cleaned = cleaned.Substring(7);
                    if (cleaned.StartsWith("```"))
                        cleaned = cleaned.Substring(3);
                    if (cleaned.EndsWith("```"))
                        cleaned = cleaned.Substring(0, cleaned.Length - 3);
                    cleaned = cleaned.Trim();
                    var featuredImage = await _ai.GenerateFeaturedImageAsync(title,
                        cleaned.Length > 100 ? cleaned.Substring(0, 100) : cleaned);

                    var newContent = new EducationalContent
                    {
                        Title = title,
                        BodyHtml = cleaned,
                        FeaturedImageUrl = featuredImage,
                        SourceUrl = normalizedUrl,
                        Category = "آموزشی",
                        Tags = "شیرینی, کیک, آموزش",
                        IsPublished = false,
                        IsApproved = false,
                        CreatedAt = DateTime.UtcNow
                    };

                    await _storage.SaveContentAsync(newContent);
                    _logger.LogInformation($"✅ Article '{title}' saved successfully with image.");

                    await Task.Delay(TimeSpan.FromSeconds(2));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"❌ Error processing {url}");
                }
            }
        }

        private string NormalizeUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return url;
            url = url.Trim();
            if (url.EndsWith("/"))
                url = url.Substring(0, url.Length - 1);
            return url;
        }
    }
}