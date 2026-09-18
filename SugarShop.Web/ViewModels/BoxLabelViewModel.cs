namespace SugarShop.Web.ViewModels
{
    /// <summary>
    /// داده‌های برچسب چاپی وزن‌کشی جعبه‌ها: برای هر جعبه، ریز وزن ردیف‌های شیرینی
    /// و وزن/قیمت نهایی جعبه (همان اعدادی که در پنل و فاکتور استفاده می‌شود).
    /// </summary>
    public class BoxLabelViewModel
    {
        public string StoreName { get; set; } = "";
        public string StorePhone { get; set; } = "";
        public int OrderId { get; set; }
        public string OrderCode { get; set; } = "";
        public string CustomerName { get; set; } = "";
        public string CustomerPhone { get; set; } = "";
        public string OrderDate { get; set; } = "";
        public string PrintDate { get; set; } = "";
        public List<BoxLabelBox> Boxes { get; set; } = new();
    }

    public class BoxLabelBox
    {
        public string BoxTitle { get; set; } = "";
        public int BoxNumber { get; set; }
        public int BoxCount { get; set; }
        public List<BoxLabelRow> Rows { get; set; } = new();

        /// <summary>جمع وزن ردیف‌های همین جعبه (گرم).</summary>
        public int RowsWeightGrams { get; set; }

        /// <summary>جمع قیمت ردیف‌های همین جعبه (تومان).</summary>
        public decimal RowsPrice { get; set; }

        /// <summary>وزن نهایی ثبت‌شده برای جعبه (گرم) — مبنای قیمت‌گذاری.</summary>
        public int FinalWeightGrams { get; set; }

        /// <summary>قیمت نهایی جعبه (تومان).</summary>
        public decimal FinalPrice { get; set; }

        /// <summary>آیا وزن/قیمت نهایی از قبل در پنل ثبت شده است؟ (در غیر این صورت هنوز وزن‌کشی نشده)</summary>
        public bool IsFinalized { get; set; }

        /// <summary>یادداشت وزن‌کشی جعبه (اختیاری).</summary>
        public string? Notes { get; set; }

        /// <summary>متن داخل QR برچسب: شناسه سفارش، نام جعبه، شماره جعبه و وزن نهایی.</summary>
        public string QrPayload { get; set; } = "";
    }

    public class BoxLabelRow
    {
        public string Name { get; set; } = "";
        public int Quantity { get; set; }

        /// <summary>وزن این ردیف (گرم).</summary>
        public int WeightGrams { get; set; }

        /// <summary>آیا این عدد «وزن تقریبی» است و هنوز وزن‌کشی نشده؟</summary>
        public bool IsApproximate { get; set; }

        public decimal PricePerKg { get; set; }
        public decimal RowPrice { get; set; }
    }
}
