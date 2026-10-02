namespace SugarShop.Web.ViewModels
{
    /// <summary>
    /// ظرفیت نوع جعبه‌ای که یک جعبه سفارش با آن پر شده است؛ مبنای کنترل
    /// «وزن بیشتر از ظرفیت جعبه» در پنل مدیریت.
    /// </summary>
    public class BoxCapacityViewModel
    {
        public int BoxTypeId { get; set; }
        public string TypeTitle { get; set; } = "";

        /// <summary>ظرفیت وزنی جعبه بر حسب گرم.</summary>
        public int CapacityGrams { get; set; }

        /// <summary>حداکثر تعداد ردیف مجاز در این جعبه.</summary>
        public int MaxRows { get; set; }
    }
}
