using System.ComponentModel.DataAnnotations;

namespace SugarShop.Web.ViewModels.Account
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "نام کاربری یا ایمیل الزامی است")]
        [Display(Name = "نام کاربری یا ایمیل")]
        public string Username { get; set; } = "";

        [Required(ErrorMessage = "رمز عبور الزامی است")]
        [DataType(DataType.Password)]
        [Display(Name = "رمز عبور")]
        public string Password { get; set; } = "";

        [Display(Name = "مرا به خاطر بسپار")]
        public bool RememberMe { get; set; }

        public string? ReturnUrl { get; set; }
    }
}