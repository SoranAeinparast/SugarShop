using System.Text;

namespace SugarShop.Web.Helpers
{
    /// <summary>
    /// تبدیل عدد به حروف فارسی (برای نوشتن مبلغ قابل پرداخت در فاکتور).
    /// از واحد ریال پشتیبانی می‌کند و تا میلیاردها را پوشش می‌دهد.
    /// </summary>
    public static class NumberToWordsFa
    {
        private static readonly string[] Ones =
        {
            "", "یک", "دو", "سه", "چهار", "پنج", "شش", "هفت", "هشت", "نه",
            "ده", "یازده", "دوازده", "سیزده", "چهارده", "پانزده", "شانزده", "هفده", "هجده", "نوزده"
        };

        private static readonly string[] Tens =
        {
            "", "", "بیست", "سی", "چهل", "پنجاه", "شصت", "هفتاد", "هشتاد", "نود"
        };

        private static readonly string[] Hundreds =
        {
            "", "صد", "دویست", "سیصد", "چهارصد", "پانصد", "ششصد", "هفتصد", "هشتصد", "نهصد"
        };

        private static readonly string[] Scales = { "", "هزار", "میلیون", "میلیارد" };

        public static string Convert(long number)
        {
            if (number == 0) return "صفر";
            if (number < 0) return "منفی " + Convert(-number);

            var sb = new StringBuilder();
            int scaleIndex = 0;
            while (number > 0)
            {
                int part = (int)(number % 1000);
                if (part != 0)
                {
                    var partText = ThreeDigits(part);
                    if (Scales[scaleIndex].Length > 0)
                        partText += " " + Scales[scaleIndex];

                    if (sb.Length > 0)
                        sb.Insert(0, " و ");
                    sb.Insert(0, partText);
                }
                number /= 1000;
                scaleIndex++;
            }
            return sb.ToString();
        }

        private static string ThreeDigits(int n)
        {
            var sb = new StringBuilder();
            int h = n / 100;
            int r = n % 100;
            if (h > 0) sb.Append(Hundreds[h]);
            if (r > 0)
            {
                if (h > 0) sb.Append(" و ");
                if (r < 20) sb.Append(Ones[r]);
                else
                {
                    sb.Append(Tens[r / 10]);
                    if (r % 10 > 0) sb.Append(" و " + Ones[r % 10]);
                }
            }
            return sb.ToString();
        }
    }
}