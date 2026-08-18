using System.ComponentModel.DataAnnotations;

namespace SugarShop.Web.Areas.Admin.ViewModels
{
    public class CategoryHeaderSettingsViewModel
    {
        public int Id { get; set; }

        [Required]
        public int CategoryId { get; set; }

        // عنوان‌ها
        public string? DefaultTitle { get; set; }
        public string? DefaultSubtitle { get; set; }
        public string? BoxSelectionSubtitle { get; set; }

        // تصویر پس‌زمینه
        public string? BackgroundImagePath { get; set; }

        // رنگ‌ها
        [Required]
        public string BackgroundColor { get; set; } = "#ffffff";

        [Required]
        public string TextColor { get; set; } = "#2c3e50";

        // ارتفاع
        [Range(100, 600)]
        public int Height { get; set; } = 200;

        // وضعیت‌ها
        public bool IsEnabled { get; set; } = true;
        public bool UseBackgroundImage { get; set; } = false;
    }
}
