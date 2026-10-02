using SugarShop.Domain.Entities;

namespace SugarShop.Web.ViewModels
{
    public class SiteSettingsViewModel
    {
        public SiteSetting SiteSetting { get; set; } = null!;
        public ThemeSetting ThemeSetting { get; set; } = null!;
    }
}