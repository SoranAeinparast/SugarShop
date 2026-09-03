using System.ComponentModel.DataAnnotations;

namespace SugarShop.Web.ViewModels
{
    public class ZibalGatewaySettingsViewModel
    {
        [Display(Name = "کلید پذیرنده (Merchant ID)")]
        public string? MerchantId { get; set; }

        [Display(Name = "حالت درگاه")]
        public string Mode { get; set; } = "Test";

        [Display(Name = "فعال‌سازی درگاه")]
        public bool IsActive { get; set; }
    }
}
