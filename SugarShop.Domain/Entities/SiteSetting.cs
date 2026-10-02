using System;

namespace SugarShop.Domain.Entities
{
    public class SiteSetting
    {
        public int Id { get; set; }
        public string? SiteTitle { get; set; }
        public string? SiteDescription { get; set; }
        public string? LogoPath { get; set; }
        public string? FaviconPath { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }

        // ✅ اطلاعات فروشنده برای فاکتور
        public string? EconomicCode { get; set; }
        public string? PostalCode { get; set; }
        public string? WorkingHours { get; set; }
        public string? InstagramUrl { get; set; }
        public string? TelegramUrl { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public string? WhatsAppUrl { get; set; }
        public string? baleUrl { get; set; }
        public string? rubikaUrl { get; set; }
        public string? eitaaUrl { get; set; }
        public string? soroushUrl { get; set; }
        public string? CertificationsJson { get; set; }
        public string? AboutShortText { get; set; }
        public string? QuickLinksJson { get; set; }
        public string? FooterCopyrightText { get; set; }
        public bool IsGalleryEnabled { get; set; } = true;
        public bool PromoEnabled { get; set; } = true;
        public string? PromoBadgeText { get; set; }
        public string? PromoTitle { get; set; }
        public string? PromoText { get; set; }
        public string? PromoButton1Text { get; set; }
        public string? PromoButton1Url { get; set; } 
        public string? PromoButton2Text { get; set; }
        public string? PromoButton2Url { get; set; } 
        public string? PromoImagePath { get; set; }
        public string? PromoBgColor { get; set; }
        public string? PromoBadgeTextColor { get; set; }
        public string? PromoTitleColor { get; set; }
        public string? PromoTextColor { get; set; }
        public string? PromoButton1BgColor { get; set; }
        public string? PromoButton1TextColor { get; set; }
        public string? PromoButton2BgColor { get; set; } 
        public string? PromoButton2TextColor { get; set; }
        public string? PromoSliderImages { get; set; }
        public bool PromoSpecialEffectEnabled { get; set; } = false; 
        public string? ContactPageTitle { get; set; }          
        public string? ContactPageSubtitle { get; set; }     
        public string? ContactFormTitle { get; set; } 
        public string? ContactFormSubtitle { get; set; } 
        public string? MapEmbedUrl { get; set; } 
        public string? MapLatitude { get; set; }
        public string? MapLongitude { get; set; }
        public string? MapLocationName { get; set; }
        public string? ContactSuccessMessage { get; set; }

        /// <summary>
        /// آستانه ارسال رایگان (تومان). اگر مبلغ کالاهای سفارش به این مقدار برسد، هزینه پیک صفر محاسبه میشود.
        /// مقدار صفر یعنی این سیاست غیرفعال است.
        /// </summary>
        public decimal FreeDeliveryThreshold { get; set; }
    }
}
