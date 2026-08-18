using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SugarShop.Domain.Entities;
using SugarShop.Infrastructure.Persistence.Sales;
using SugarShop.Web.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SugarShop.Web.Services
{
    public class ContentSchedulerService
    {
        private readonly IRecurringJobManager _recurringJobManager;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ContentSchedulerService> _logger;

        public ContentSchedulerService(
            IRecurringJobManager recurringJobManager,
            IServiceProvider serviceProvider,
            ILogger<ContentSchedulerService> logger)
        {
            _recurringJobManager = recurringJobManager;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public async Task ScheduleContentFetching()
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<SugarShopSalesDbContext>();

            var settings = await context.AIContentSettings.FirstOrDefaultAsync() ?? new AIContentSettings();
            var cron = settings.ScheduleCron ?? "0 8 * * *";

            _recurringJobManager.AddOrUpdate(
                "fetch-and-publish-content",
                () => OrchestrateContent(),
                cron,
                new RecurringJobOptions
                {
                    TimeZone = TimeZoneInfo.Local
                });

            _logger.LogInformation("Scheduled content fetching with cron: {Cron}", cron);
        }

        public async Task OrchestrateContent()
        {
            _logger.LogInformation("Starting content orchestration...");

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<SugarShopSalesDbContext>();
                var orchestrator = scope.ServiceProvider.GetRequiredService<IContentOrchestratorService>();
                var sources = await context.ContentSources
                    .Where(s => s.IsActive)
                    .OrderBy(s => s.Priority)
                    .Take(10)
                    .Select(s => s.Url)
                    .ToListAsync();

                if (!sources.Any())
                {
                    _logger.LogWarning("No active content sources found.");
                    return;
                }

                _logger.LogInformation("Found {Count} content sources.", sources.Count);
                await orchestrator.RunContentPipelineAsync(sources);

                _logger.LogInformation("Content orchestration completed successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during content orchestration.");
                throw;
            }
        }
    }
}