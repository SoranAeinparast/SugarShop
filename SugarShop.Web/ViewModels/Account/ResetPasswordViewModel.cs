using System.ComponentModel.DataAnnotations;

namespace SugarShop.Web.ViewModels.Account
{
    public class ResetPasswordViewModel
    {
        [Required(ErrorMessage = "کد بازیابی نامعتبر است")]
        [Display(Name = "کد بازیابی")]
        public string? Code { get; set; }
        [Required(ErrorMessage = "شناسه کاربر نامعتبر است")]
        [Display(Name = "شناسه کاربر")]
        public string? UserId { get; set; }
        [Required(ErrorMessage = "ایمیل الزامی است")]
        [EmailAddress(ErrorMessage = "فرمت ایمیل وارد شده معتبر نیست")]
        [Display(Name = "ایمیل")]
        [StringLength(256, ErrorMessage = "ایمیل نمی‌تواند بیشتر از 256 کاراکتر باشد")]
        public string Email { get; set; } = "";
        [Required(ErrorMessage = "رمز عبور جدید الزامی است")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "رمز عبور باید حداقل 6 کاراکتر باشد")]
        [DataType(DataType.Password)]
        [Display(Name = "رمز عبور جدید")]
        public string Password { get; set; } = "";
        [Required(ErrorMessage = "تکرار رمز عبور الزامی است")]
        [DataType(DataType.Password)]
        [Display(Name = "تکرار رمز عبور جدید")]
        [Compare("Password", ErrorMessage = "رمز عبور و تکرار آن مطابقت ندارند")]
        public string ConfirmPassword { get; set; } = "";
    }
}