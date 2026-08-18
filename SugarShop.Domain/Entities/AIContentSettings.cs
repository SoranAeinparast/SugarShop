using System;

namespace SugarShop.Domain.Entities
{
    public class AIContentSettings
    {
        public int Id { get; set; }
        public string? OpenAIApiKey { get; set; }
        public string? ModelName { get; set; } = "gpt-4o-mini";
        public string? ImageModelName { get; set; } = "dall-e-3";
        public bool AutoPublishEnabled { get; set; } = false;
        public bool RequireAdminApproval { get; set; } = true;
        public string? ScheduleCron { get; set; } = "0 8 * * *";
        public int MaxArticlesPerRun { get; set; } = 5;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}