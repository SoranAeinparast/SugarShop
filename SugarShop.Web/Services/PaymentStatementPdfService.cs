using System.Globalization;
using System.Reflection;
using System.Text;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SugarShop.Web.Extensions;
using SugarShop.Web.ViewModels;

namespace SugarShop.Web.Services
{
    /// <summary>
    /// ساخت فایل PDF واقعی صورت‌حساب پرداخت (بدون نیاز به پنجره چاپ مرورگر).
    /// محتوا از همان <see cref="StagePaymentViewModel"/> می‌آید که صفحه «تأیید و پرداخت» و
    /// خروجی اکسل را می‌سازد؛ پس هر سه سند و مبلغی که درگاه دریافت می‌کند همیشه یکی هستند.
    /// فونت وزیرمتن (SIL OFL) داخل خود اسمبلی تعبیه شده تا روی سروری که هیچ فونت فارسی
    /// نصب‌شده‌ای ندارد هم متن فارسی درست و چسبیده رندر شود.
    /// </summary>
    public class PaymentStatementPdfService
    {
        /// <summary>نام خانواده فونت تعبیه‌شده؛ در صورت نبود، فونت‌های سیستمی جایگزین می‌شوند.</summary>
        private static readonly string[] FontFamilies = { "Vazirmatn", "Tahoma", "Arial", "DejaVu Sans" };

        private static readonly object FontSync = new();
        private static bool _fontsReady;

        private readonly ILogger<PaymentStatementPdfService> _logger;

        public PaymentStatementPdfService(ILogger<PaymentStatementPdfService> logger) => _logger = logger;

        public byte[] Build(StagePaymentViewModel model)
        {
            EnsureFontsAndLicense(_logger);

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(11, Unit.Millimetre);
                    page.PageColor(Colors.White);
                    // کل سند راست‌به‌چپ است؛ DirectionFromRightToLeft هم ترتیب متن و هم
                    // جای اعداد/علامت‌ها را با الگوریتم دوجهته‌ی استاندارد می‌چیند.
                    page.DefaultTextStyle(style => style
                        .FontFamily(FontFamilies)
                        .FontSize(9)
                        .FontColor(Colors.Black)
                        .DirectionFromRightToLeft());
                    page.ContentFromRightToLeft();

                    // سربرگ و مشخصات در Header می‌نشینند تا در سندهای چندصفحه‌ای هم
                    // صفحه‌های بعدی شناسنامه (شماره سفارش و نام فروشگاه) داشته باشند.
                    page.Header().Column(column =>
                    {
                        column.Spacing(5);
                        column.Item().Element(c => ComposeHeader(c, model));
                        column.Item().Element(c => ComposeMeta(c, model));
                    });

                    page.Content().PaddingTop(6).Element(c => ComposeBody(c, model));

                    page.Footer().Element(c => ComposeFooter(c, model));
                });
            }).GeneratePdf();
        }

        // ───────────────────────── سربرگ و مشخصات ─────────────────────────

        private static void ComposeHeader(IContainer container, StagePaymentViewModel model)
        {
            container.Border(1.2f).BorderColor(Colors.Black).Padding(7).Row(row =>
            {
                row.RelativeItem().Column(column =>
                {
                    column.Item().Text(Txt(model.StoreName)).FontSize(14).Bold();
                    column.Item().PaddingTop(1).Text(StageTitle(model)).FontSize(9).FontColor(Colors.Grey.Darken3);
                    if (!string.IsNullOrWhiteSpace(model.StorePhone))
                        column.Item().Text($"تلفن: {LtrPhone(model.StorePhone.ToPersianNumber())}").FontSize(9).FontColor(Colors.Grey.Darken3);
                });

                row.ConstantItem(8);

                row.ConstantItem(125).Border(1.2f).BorderColor(Colors.Black).Padding(5).Column(column =>
                {
                    column.Item().AlignCenter().Text("شماره سفارش").FontSize(8).FontColor(Colors.Grey.Darken3);
                    column.Item().AlignCenter().Text(model.OrderCode.ToPersianNumber()).FontSize(13).Bold();
                });
            });
        }

        private static void ComposeMeta(IContainer container, StagePaymentViewModel model)
        {
            container.PaddingTop(2).Row(row =>
            {
                void Cell(string label, string value) =>
                    row.RelativeItem().Text(text =>
                    {
                        text.Span($"{label} ").FontColor(Colors.Grey.Darken2);
                        text.Span(value).SemiBold();
                    });

                Cell("مشتری:", string.IsNullOrWhiteSpace(model.CustomerName) ? "-" : Txt(model.CustomerName));
                Cell("تاریخ سفارش:", model.OrderDate.ToPersianNumber());
                Cell("شیوه تحویل:", Txt(model.DeliveryMethodText));
                Cell("تاریخ صدور سند:", model.PrintDate.ToPersianNumber());
            });
        }

        // ───────────────────────── بدنه سند ─────────────────────────

        private static void ComposeBody(IContainer container, StagePaymentViewModel model)
        {
            var hasFinalizedBoxes = model.Boxes.Any(b => b.IsFinalized);

            container.Column(column =>
            {
                column.Spacing(6);

                if (model.Boxes.Any())
                {
                    column.Item().Element(c => SectionTitle(c,
                        hasFinalizedBoxes ? "ریز وزن و قیمت جعبه‌های شیرینی" : "جعبه‌های شیرینی (پرداخت در مرحله دوم، پس از وزن‌کشی)"));

                    foreach (var box in model.Boxes)
                    {
                        var current = box;
                        column.Item().Element(c => ComposeBox(c, current));
                    }
                }

                if (model.Products.Any())
                {
                    column.Item().Element(c => SectionTitle(c, "محصولات قیمت‌ثابت"));
                    column.Item().Element(c => ComposeProducts(c, model));
                }

                column.Item().Element(c => SectionTitle(c, "جمع‌بندی مبلغ"));
                column.Item().Element(c => ComposeSummary(c, model));

                column.Item().PaddingTop(14).Row(row =>
                {
                    row.RelativeItem().MinHeight(64).Border(0.7f).BorderColor(Colors.Black)
                        .AlignMiddle().AlignCenter().Text("امضای مشتری").FontSize(9);
                    row.ConstantItem(10);
                    row.RelativeItem().MinHeight(64).Border(0.7f).BorderColor(Colors.Black)
                        .AlignMiddle().AlignCenter().Text("مهر و امضای فروشگاه").FontSize(9);
                });
            });
        }

        private static void SectionTitle(IContainer container, string title)
        {
            container.PaddingTop(2).BorderBottom(1).BorderColor(Colors.Black).PaddingBottom(2)
                .Text(title).FontSize(10).Bold();
        }

        private static void ComposeBox(IContainer container, StagePaymentBox box)
        {
            container.Column(column =>
            {
                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(26);   // ردیف
                        columns.RelativeColumn(4);    // نام شیرینی
                        columns.ConstantColumn(38);   // تعداد
                        columns.ConstantColumn(80);   // وزن
                        columns.ConstantColumn(95);   // قیمت
                    });

                    table.Header(header =>
                    {
                        header.Cell().Element(HeaderCell).Text("ردیف");
                        header.Cell().Element(HeaderCell).Text("شیرینی");
                        header.Cell().Element(HeaderCell).Text("تعداد");
                        header.Cell().Element(HeaderCell).Text("وزن (گرم)");
                        header.Cell().Element(HeaderCell).Text("قیمت این ردیف");
                    });

                    var rowNumber = 0;
                    foreach (var line in box.Rows)
                    {
                        rowNumber++;
                        table.Cell().Element(BodyCell).Text(rowNumber.ToPersianNumber());
                        table.Cell().Element(NameCell).Text(Txt(line.Name));
                        table.Cell().Element(BodyCell).Text(line.Quantity.ToPersianNumber());
                        table.Cell().Element(BodyCell).Text(line.WeightGrams.HasValue
                            ? $"{line.WeightGrams.Value.ToPersianNumber()}{(line.IsApproximate ? " (تقریبی)" : "")}"
                            : "-");
                        table.Cell().Element(BodyCell).Text(Money(line.TotalPrice));
                    }

                    // جمع ردیف‌های همین جعبه
                    table.Cell().ColumnSpan(3).Element(FootNameCell)
                        .Text($"جمع ردیف‌های {Txt(box.BoxTitle)} (جعبه {box.BoxNumber.ToPersianNumber()} از {box.BoxCount.ToPersianNumber()})");
                    table.Cell().Element(FootCell).Text($"{box.RowsWeightGrams.ToPersianNumber()} گرم");
                    table.Cell().Element(FootCell).Text(Money(box.RowsPrice));

                    if (box.IsFinalized)
                    {
                        table.Cell().ColumnSpan(3).Element(FinalNameCell)
                            .Text("وزن نهایی و مبلغ نهایی این جعبه (مبنای پرداخت)");
                        table.Cell().Element(FinalCell).Text($"{(box.FinalWeightGrams ?? 0).ToPersianNumber()} گرم");
                        table.Cell().Element(FinalCell).Text(Money(box.FinalPrice ?? 0));
                    }
                });

                if (!box.IsFinalized)
                {
                    column.Item().PaddingTop(2).Text(
                        "این جعبه هنوز وزن‌کشی/ثبت نشده است؛ وزن و قیمت بالا تقریبی است و مرحله بعد تسویه می‌شود.")
                        .FontSize(8).FontColor(Colors.Grey.Darken2);
                }
                else if (box.FinalPrice.HasValue && box.RowsPrice != box.FinalPrice.Value)
                {
                    column.Item().PaddingTop(2).Text(
                        $"مبلغ نهایی این جعبه توسط فروشگاه ثبت شده است؛ جمع ردیف‌ها ({Money(box.RowsPrice)}) ممکن است کمی تفاوت داشته باشد و مبنای دریافتی، مبلغ نهایی جعبه است.")
                        .FontSize(8).FontColor(Colors.Grey.Darken2);
                }
            });
        }

        private static void ComposeProducts(IContainer container, StagePaymentViewModel model)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(4);
                    columns.ConstantColumn(45);
                    columns.ConstantColumn(100);
                    columns.ConstantColumn(100);
                });

                table.Header(header =>
                {
                    header.Cell().Element(HeaderCell).Text("محصول");
                    header.Cell().Element(HeaderCell).Text("تعداد");
                    header.Cell().Element(HeaderCell).Text("قیمت واحد");
                    header.Cell().Element(HeaderCell).Text("جمع");
                });

                foreach (var line in model.Products)
                {
                    table.Cell().Element(NameCell).Text(Txt(line.Name));
                    table.Cell().Element(BodyCell).Text(line.Quantity.ToPersianNumber());
                    table.Cell().Element(BodyCell).Text(Money(line.UnitPrice));
                    table.Cell().Element(BodyCell).Text(Money(line.TotalPrice));
                }
            });
        }

        private static void ComposeSummary(IContainer container, StagePaymentViewModel model)
        {
            var hasFinalizedBoxes = model.Boxes.Any(b => b.IsFinalized);

            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(4);
                    columns.ConstantColumn(140);
                });

                // هر سطر جمع‌بندی: یک سلول عنوان (راست‌چین) و یک سلول مبلغ؛
                // «bold» برای سطرهای جمع (جمع کالاها، مبلغ کل) و «payable» برای سطر نهایی مبلغ قابل پرداخت.
                void Row(string title, string value, bool bold = false, bool payable = false)
                {
                    Func<IContainer, IContainer> cell = payable ? PayableCell : (bold ? TotalCell : BodyCell);

                    var titleCell = table.Cell().Element(cell).PaddingHorizontal(4).AlignRight();
                    if (bold || payable) titleCell.DefaultTextStyle(t => t.Bold()).Text(title);
                    else titleCell.Text(title);

                    var valueCell = table.Cell().Element(cell).PaddingHorizontal(4).AlignRight();
                    if (bold || payable) valueCell.DefaultTextStyle(t => t.Bold()).Text(value);
                    else valueCell.Text(value);
                }

                if (model.FinalizedBoxTotal > 0)
                    Row(model.HasUnfinalizedBoxes ? "جمع جعبه‌های وزن‌کشی‌شده (فقط جعبه‌های نهایی‌شده)" : "جمع جعبه‌های وزن‌کشی‌شده",
                        Money(model.FinalizedBoxTotal));

                if (model.Products.Any())
                    Row("جمع محصولات قیمت‌ثابت", Money(model.ProductTotal));

                Row("جمع کالاها", Money(model.GoodsTotal), bold: true);

                if (model.DiscountAmount > 0)
                    Row("کد تخفیف", $"\u2212 {Money(model.DiscountAmount)}");

                if (model.IsDeliveryFree)
                {
                    table.Cell().Element(BodyCell).PaddingHorizontal(4).AlignRight().Text("هزینه ارسال با پیک (ارسال رایگان)");
                    table.Cell().Element(BodyCell).PaddingHorizontal(4).AlignRight().Text(text =>
                    {
                        text.Span(Money(model.StoredDeliveryFee)).Strikethrough().FontColor(Colors.Grey.Darken1);
                        text.Span($"  {Money(0)}");
                    });
                }
                else
                {
                    Row("هزینه ارسال با پیک", Money(model.DeliveryFee));
                }

                Row("مبلغ کل سفارش تا این لحظه", Money(model.GrandTotal), bold: true);

                if (model.PaidTotal > 0)
                {
                    table.Cell().Element(BodyCell).PaddingHorizontal(4).AlignRight().Column(column =>
                    {
                        column.Item().Text("پرداخت‌های قبلی");
                        foreach (var payment in model.PreviousPayments)
                            column.Item().Text($"{payment.Date.ToPersianNumber()} — {Txt(payment.Provider)} — {Money(payment.Amount)}")
                                .FontSize(8).FontColor(Colors.Grey.Darken2);
                    });
                    table.Cell().Element(BodyCell).PaddingHorizontal(4).AlignRight().Text($"\u2212 {Money(model.PaidTotal)}");
                }

                Row(hasFinalizedBoxes ? "مبلغ قابل پرداخت در این مرحله" : "مبلغ قابل پرداخت در مرحله اول",
                    Money(model.PayableNow), payable: true);
            });
        }

        private static void ComposeFooter(IContainer container, StagePaymentViewModel model)
        {
            container.PaddingTop(4).BorderTop(0.7f).BorderColor(Colors.Grey.Darken1).PaddingTop(3).Text(text =>
            {
                text.Span("این سند ریز مبلغ پرداخت سفارش شماست و اعداد آن از همان محاسبه‌ای می‌آید که مبلغ دریافتی درگاه را تعیین می‌کند.")
                    .FontSize(8).FontColor(Colors.Grey.Darken2);

                if (model.HasUnfinalizedBoxes)
                    text.Span(" جعبه‌های شیرینی که هنوز وزن‌کشی نشده‌اند در این مبلغ لحاظ نشده‌اند و پس از وزن‌کشی، صورت‌حساب مرحله بعد با همین ریز جزئیات صادر و مبلغ آن دریافت می‌شود.")
                        .FontSize(8).FontColor(Colors.Grey.Darken2);
            });
        }

        // ───────────────────────── سلول‌های جدول ─────────────────────────

        private static IContainer HeaderCell(IContainer container) => container
            .Border(0.6f).BorderColor(Colors.Black).Background(Colors.Grey.Lighten2)
            .PaddingVertical(3).PaddingHorizontal(3).AlignMiddle().AlignCenter().DefaultTextStyle(t => t.Bold());

        private static IContainer BodyCell(IContainer container) => container
            .Border(0.6f).BorderColor(Colors.Grey.Darken1).PaddingVertical(3).PaddingHorizontal(3)
            .AlignMiddle().AlignCenter();

        private static IContainer NameCell(IContainer container) => BodyCell(container).AlignRight();

        private static IContainer FootCell(IContainer container) => container
            .Border(0.6f).BorderColor(Colors.Black).Background(Colors.Grey.Lighten3)
            .PaddingVertical(3).PaddingHorizontal(3).AlignMiddle().AlignCenter().DefaultTextStyle(t => t.Bold());

        private static IContainer FootNameCell(IContainer container) => FootCell(container).AlignRight();

        private static IContainer FinalCell(IContainer container) => container
            .Border(0.6f).BorderColor(Colors.Black).Background(Colors.Green.Lighten4)
            .PaddingVertical(3).PaddingHorizontal(3).AlignMiddle().AlignCenter().DefaultTextStyle(t => t.Bold());

        private static IContainer FinalNameCell(IContainer container) => FinalCell(container).AlignRight();

        private static IContainer TotalCell(IContainer container) => container
            .Border(0.6f).BorderColor(Colors.Black).Background(Colors.Grey.Lighten3)
            .PaddingVertical(3).PaddingHorizontal(3).AlignMiddle();

        private static IContainer PayableCell(IContainer container) => container
            .Border(0.6f).BorderColor(Colors.Black).Background(Colors.Green.Lighten4)
            .PaddingVertical(3).PaddingHorizontal(3).AlignMiddle();

        // ───────────────────────── کمک‌تابع‌ها ─────────────────────────

        /// <summary>مبلغ با ارقام فارسی و جداکننده هزارگان فارسی (هم‌شکل با بقیه سندها).</summary>
        private static string Money(decimal amount)
            => amount.ToString("N0", CultureInfo.InvariantCulture).Replace(",", "\u066C").ToPersianNumber() + " تومان";

        /// <summary>
        /// پاک‌سازی متن‌های آمده از داده برای چاپ: حذف شکلک‌ها (emoji) و کاراکترهای کنترلی.
        /// فونت فارسی گلیف شکلک ندارد؛ روی سروری که فونت emoji نصب نیست، به‌جای شکلک مربع خالی چاپ می‌شد.
        /// </summary>
        private static string Txt(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;

            var builder = new StringBuilder(value.Length);
            foreach (var ch in value)
            {
                if (char.IsSurrogate(ch)) continue;              // همه شکلک‌های خارج از BMP
                if (ch == '\uFE0F' || ch == '\u200D') continue; // Variation Selector-16 و ZWJ
                if (ch >= '\u2600' && ch <= '\u27BF') continue; // بلوک Miscellaneous Symbols (⚖️ ✅ ⚠️)
                if (ch >= '\u2B00' && ch <= '\u2BFF') continue; // ⬇️ ⭐ ...
                if (char.IsControl(ch)) continue;
                builder.Append(ch);
            }

            return string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }

        /// <summary>
        /// شماره تلفن (اعداد جدا‌شده با خط تیره): موتور دوجهته‌ی QuestPDF خط تیره را جداکننده می‌بیند و
        /// ترتیب اعداد را برمی‌گرداند (۰۲۱-۱۲۳۴۵۶۷۸ به شکل ۱۲۳۴۵۶۷۸-۰۲۱ چاپ می‌شد). یک نشانه‌ی LRM
        /// در ابتدای رشته این ترتیب را درست می‌کند؛ خود نشانه در سند دیده نمی‌شود.
        /// </summary>
        private static string LtrPhone(string? value)
        {
            var text = Txt(value);
            return text.Length == 0 ? text : "\u200E" + text;
        }

        private static string StageTitle(StagePaymentViewModel model)
        {
            var hasFinalized = model.Boxes.Any(b => b.IsFinalized);
            var hasPending = model.Boxes.Any(b => !b.IsFinalized);

            if (!hasFinalized && hasPending) return "صورت‌حساب پرداخت مرحله اول (بخش قیمت‌ثابت)";
            if (hasPending) return "صورت‌حساب پرداخت مرحله دوم (بخشی از جعبه‌ها)";
            return "صورت‌حساب پرداخت مرحله دوم (جعبه‌های وزن‌کشی‌شده)";
        }

        /// <summary>
        /// ثبت مجوز QuestPDF (نسخه Community برای کسب‌وکارهای زیر یک میلیون دلار درآمد سالانه)
        /// و بارگذاری فونت‌های تعبیه‌شده؛ فقط یک‌بار در طول عمر برنامه انجام می‌شود.
        /// </summary>
        private static void EnsureFontsAndLicense(ILogger logger)
        {
            if (_fontsReady) return;

            lock (FontSync)
            {
                if (_fontsReady) return;

                QuestPDF.Settings.License = LicenseType.Community;
                QuestPDF.Settings.UseSystemFonts = true;

                // فونت‌های جایگزین (Tahoma/Arial/DejaVu) روی همه سرورها نصب نیستند؛ خاموش‌کردن این گزینه
                // باعث می‌شود نبودشان تولید PDF را از کار نیندازد و اولین فونت موجود انتخاب شود
                // (که همیشه فونت وزیرمتن تعبیه‌شده است).
                QuestPDF.Settings.ThrowOnMissingFontFamilies = false;

                var assembly = typeof(PaymentStatementPdfService).Assembly;
                var registered = 0;

                foreach (var resourceName in GetFontResources(assembly))
                {
                    try
                    {
                        using var stream = assembly.GetManifestResourceStream(resourceName);
                        if (stream == null) continue;

                        using var buffer = new MemoryStream();
                        stream.CopyTo(buffer);
                        FontManager.RegisterFontFromBinaryData(buffer.ToArray());
                        registered++;
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "ثبت فونت تعبیه‌شده {Font} برای تولید PDF ناموفق بود", resourceName);
                    }
                }

                if (registered == 0)
                    logger.LogError("هیچ فونت تعبیه‌شده‌ای برای تولید PDF پیدا نشد؛ متن فارسی سند ممکن است ناخوانا چاپ شود");

                _fontsReady = true;
            }
        }

        private static IEnumerable<string> GetFontResources(Assembly assembly)
            => assembly.GetManifestResourceNames()
                .Where(name => name.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase)
                               && name.Contains("Fonts", StringComparison.OrdinalIgnoreCase))
                .OrderBy(name => name, StringComparer.Ordinal);
    }
}
