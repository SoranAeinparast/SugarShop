namespace SugarShop.Web.ViewModels
{
    /// <summary>
    /// صفحه «تأیید و پرداخت مرحله دوم»: پیش از رفتن به درگاه، ریز وزن و قیمت هر ردیف جعبه
    /// و سهم هر بخش در مبلغ نهایی به مشتری نشان داده می‌شود تا بداند مبلغ از کجا آمده است.
    /// همه اعداد از <c>OrderPricingService</c> (تنها منبع محاسبه مبلغ) و مقادیر ثبت‌شده
    /// روی ردیف‌های سفارش می‌آید، پس با مبلغی که از درگاه دریافت می‌شود یکی است.
    /// </summary>
    public class StagePaymentViewModel
    {
        public int OrderId { get; set; }
        public string OrderCode { get; set; } = "";
        public string OrderDate { get; set; } = "";
        public string DeliveryMethodText { get; set; } = "";

        // ── اطلاعات سربرگ سند چاپی/اکسل ──
        public string StoreName { get; set; } = "";
        public string StorePhone { get; set; } = "";
        public string CustomerName { get; set; } = "";
        public string PrintDate { get; set; } = "";

        /// <summary>جعبه‌های شیرینی سفارش (ریز وزن و قیمت ردیف‌ها).</summary>
        public List<StagePaymentBox> Boxes { get; set; } = new();

        /// <summary>محصولات قیمت‌ثابت سفارش.</summary>
        public List<StagePaymentLine> Products { get; set; } = new();

        // ── جمع‌بندی مبلغ (تومان) ──
        public decimal ProductTotal { get; set; }
        public decimal FinalizedBoxTotal { get; set; }
        public decimal GoodsTotal { get; set; }
        public decimal StoredDeliveryFee { get; set; }
        public decimal DeliveryFee { get; set; }
        public bool IsDeliveryFree { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal GrandTotal { get; set; }
        public decimal PaidTotal { get; set; }

        /// <summary>مبلغی که در همین مرحله از مشتری دریافت می‌شود.</summary>
        public decimal PayableNow { get; set; }

        /// <summary>آیا هنوز جعبه‌ای وزن‌کشی نهایی نشده است؟ (مبلغ آن به مرحله بعد می‌ماند)</summary>
        public bool HasUnfinalizedBoxes { get; set; }

        /// <summary>پرداخت‌های موفق قبلی همین سفارش (برای شفاف‌سازی مبلغ باقی‌مانده).</summary>
        public List<StagePaymentPayment> PreviousPayments { get; set; } = new();

        /// <summary>
        /// توکن لینک اختصاصی، فقط وقتی سند از مسیر /s/{token} باز شده باشد پر است.
        /// در آن حالت دکمه‌های دانلود از همان توکن استفاده می‌کنند (چون مشتری وارد نشده است).
        /// </summary>
        public string? PublicToken { get; set; }

        /// <summary>تاریخ پایان اعتبار لینک اختصاصی (برای نمایش روی سند).</summary>
        public string? LinkExpiresAtText { get; set; }

        /// <summary>
        /// آیا همین حالا مبلغی برای پرداخت وجود دارد؟ (پرداخت سفارش فعال است و مبلغی باقی مانده)
        /// همین یک پرچم دکمه پرداخت را در هر دو مسیر (پس از ورود و لینک اختصاصی) کنترل می‌کند
        /// تا هیچ‌وقت دکمه‌ای که کار نمی‌کند نشان داده نشود.
        /// </summary>
        public bool CanPayNow { get; set; }

        /// <summary>
        /// سند با قصد پرداخت باز شده است (مشتری از لینک پرداخت پیامکی به این صفحه رسیده)،
        /// پس دکمه پرداخت باید برجسته و در بالای صفحه باشد.
        /// </summary>
        public bool PayFromPublicLink { get; set; }

        /// <summary>
        /// نام پکیج اپ اندروید، فقط وقتی اپ در تنظیمات فعال باشد پر است.
        /// برای دکمه «باز کردن در اپلیکیشن» لازم است (حالت مرورگرهایی که لینک‌های https را
        /// خودشان در اپ باز نمی‌کنند یا تأیید App Links روی آن گوشی انجام نشده است).
        /// </summary>

    }

    public class StagePaymentBox
    {
        public string BoxTitle { get; set; } = "";
        public int BoxNumber { get; set; }
        public int BoxCount { get; set; }

        /// <summary>ریز ردیف‌های همین جعبه (نام شیرینی، تعداد، وزن و قیمت همان ردیف).</summary>
        public List<StagePaymentLine> Rows { get; set; } = new();

        /// <summary>جمع وزن ردیف‌ها (گرم).</summary>
        public int RowsWeightGrams { get; set; }

        /// <summary>جمع قیمت ردیف‌ها (تومان).</summary>
        public decimal RowsPrice { get; set; }

        /// <summary>وزن نهایی اعلام‌شده فروشگاه (اگر وزن‌کشی شده باشد).</summary>
        public int? FinalWeightGrams { get; set; }

        /// <summary>قیمت نهایی اعلام‌شده فروشگاه (مبنای مبلغ همین جعبه).</summary>
        public decimal? FinalPrice { get; set; }

        /// <summary>آیا وزن/قیمت نهایی این جعبه ثبت شده است؟ (فقط جعبه‌های ثبت‌شده در مبلغ این مرحله می‌آیند)</summary>
        public bool IsFinalized { get; set; }
    }

    public class StagePaymentLine
    {
        public string Name { get; set; } = "";
        public int Quantity { get; set; } = 1;

        /// <summary>وزن این ردیف (گرم) — برای محصولات قیمت‌ثابت ممکن است خالی باشد.</summary>
        public int? WeightGrams { get; set; }

        /// <summary>قیمت این ردیف با وزن ثبت‌شده (بدون ضرب در تعداد).</summary>
        public decimal UnitPrice { get; set; }

        /// <summary>قیمت این ردیف ضرب در تعداد.</summary>
        public decimal TotalPrice { get; set; }

        /// <summary>آیا عدد این ردیف «تقریبی» است و فروشگاه هنوز وزن‌کشی نکرده است؟</summary>
        public bool IsApproximate { get; set; }
    }

    public class StagePaymentPayment
    {
        public decimal Amount { get; set; }
        public string Date { get; set; } = "";
        public string Provider { get; set; } = "";
    }
}
