namespace SugarShop.Web.Areas.Admin.ViewModels
{
    public class AboutUsViewModel
    {
        public int Id { get; set; }
        public string HeroTitle { get; set; } = "";
        public string HeroDescription { get; set; } = "";
        public string StoryTitle { get; set; } = "";
        public string StoryContent { get; set; } = "";
        public string VideoTitle { get; set; } = "";

        /// <summary>
        /// مقادیر کارت‌های «ارزش‌های بنیادین» — توسط ویرایشگر بصری مدیریت می‌شود.
        /// این لیست از روی JSON موجود ساخته و هنگام ذخیره دوباره به JSON تبدیل می‌شود؛
        /// بنابراین ادمین هرگز مستقیم با کد JSON سروکار ندارد.
        /// </summary>
        public List<AboutUsValueItem> Values { get; set; } = new();

        // برای سازگاری، مقدار JSON قبلی هم نگه داشته می‌شود (در صورت نیاز به ویرایش دستی)
        public string ValuesJson { get; set; } = "[]";

        public string? HeroImagePath { get; set; }
        public string? StoryImagePath { get; set; }
        public string? VideoPath { get; set; }
    }

    public class AboutUsValueItem
    {
        public string Icon { get; set; } = "bi-star";
        public string Title { get; set; } = "";
        public string Desc { get; set; } = "";
    }
}