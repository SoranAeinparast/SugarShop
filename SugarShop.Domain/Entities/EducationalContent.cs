using System;
using System.Collections.Generic;

namespace SugarShop.Domain.Entities
{
    public class EducationalContent
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string BodyHtml { get; set; } = string.Empty;
        public string? FeaturedImageUrl { get; set; }
        public string? SourceUrl { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? PublishedAt { get; set; }
        public bool IsPublished { get; set; } = false;
        public string? Category { get; set; }
        public string? Tags { get; set; }
        public string? MetaDescription { get; set; }
        public bool IsApproved { get; set; } = false;
        public DateTime? ApprovedAt { get; set; }
        public string? AdminNotes { get; set; }
        public int? SourceId { get; set; }
        public int? TopicId { get; set; }
        public virtual ContentSource? Source { get; set; }
        public virtual ContentTopic? Topic { get; set; }
    }
}