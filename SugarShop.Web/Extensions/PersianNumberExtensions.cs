using System.Text;

namespace SugarShop.Web.Extensions
{
    public static class PersianNumberExtensions
    {
        private static readonly char[] EnglishDigits = { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' };
        private static readonly char[] PersianDigits = { '۰', '۱', '۲', '۳', '۴', '۵', '۶', '۷', '۸', '۹' };

        public static string ToPersianNumber(this int number)
        {
            return number.ToString().ToPersianNumber();
        }

        public static string ToPersianNumber(this long number)
        {
            return number.ToString().ToPersianNumber();
        }

        public static string ToPersianNumber(this string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return input;

            var result = new StringBuilder(input.Length);
            foreach (char c in input)
            {
                if (char.IsDigit(c))
                {
                    var index = c - '0';
                    result.Append(PersianDigits[index]);
                }
                else
                {
                    result.Append(c);
                }
            }
            return result.ToString();
        }
    }
}