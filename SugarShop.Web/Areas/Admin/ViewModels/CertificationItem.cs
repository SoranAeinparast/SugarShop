namespace SugarShop.Web.Areas.Admin.ViewModels
{
    /// <summary>
    /// یک نماد/گواهینامه (مثل نماد اعتماد الکترونیکی) که در فوتر سایت نمایش داده می‌شود.
    /// در دیتابیس به صورت JSON در SiteSetting.CertificationsJson ذخیره می‌شود
    /// و توسط ویرایشگر بصری صفحه «تنظیمات سایت» مدیریت می‌شود (بدون نیاز به کدنویسی).
    /// </summary>
    public class CertificationItem
    {
        public string Title { get; set; } = "";
        public string ImagePath { get; set; } = "";
        public string Link { get; set; } = "";
        public string Alt { get; set; } = "";
    }
}
