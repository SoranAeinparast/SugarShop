using System.ComponentModel.DataAnnotations;

namespace SugarShop.Web.ViewModels.Account
{
    public class RegisterViewModel
    {
        [Required(ErrorMessage = "نام کاربری الزامی است")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "نام کاربری باید بین 3 تا 50 کاراکتر باشد")]
        [RegularExpression(@"^[a-zA-Z0-9_\-\.\@\s\u0600-\u06FF\uFB50-\uFDFF\uFE70-\uFEFF]+$",
            ErrorMessage = "نام کاربری فقط می‌تواند شامل حروف فارسی، انگلیسی، اعداد و کاراکترهای _ - @ . و فاصله باشد.")]
        [Display(Name = "نام کاربری")]
        public string Username { get; set; } = "";

        [Required(ErrorMessage = "نام و نام خانوادگی الزامی است")]
        [StringLength(100, ErrorMessage = "نام نمی‌تواند بیشتر از 100 کاراکتر باشد")]
        [Display(Name = "نام و نام خانوادگی")]
        public string FullName { get; set; } = "";

        [Required(ErrorMessage = "ایمیل الزامی است")]
        [EmailAddress(ErrorMessage = "ایمیل معتبر نیست")]
        [Display(Name = "ایمیل")]
        public string Email { get; set; } = "";

        [Required(ErrorMessage = "رمز عبور الزامی است")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "رمز عبور باید حداقل 6 کاراکتر باشد")]
        [DataType(DataType.Password)]
        [Display(Name = "رمز عبور")]
        public string Password { get; set; } = "";

        [Required(ErrorMessage = "تکرار رمز عبور الزامی است")]
        [DataType(DataType.Password)]
        [Display(Name = "تکرار رمز عبور")]
        [Compare("Password", ErrorMessage = "رمز عبور و تکرار آن مطابقت ندارند")]
        public string ConfirmPassword { get; set; } = "";
    }
}