using Microsoft.AspNetCore.Identity;
using System.Text.RegularExpressions;

namespace SugarShop.Web.Validators
{
    public class CustomUserValidator<TUser> : UserValidator<TUser> where TUser : class
    {
        public override async Task<IdentityResult> ValidateAsync(UserManager<TUser> manager, TUser user)
        {
            var result = await base.ValidateAsync(manager, user);

            var errors = result.Errors.ToList();
            errors.RemoveAll(e => e.Code == "InvalidUserName");
            var userName = await manager.GetUserNameAsync(user);
            if (!string.IsNullOrEmpty(userName))
            {
                var regex = new Regex(@"^[\w\-\@\. \u0600-\u06FF\uFB50-\uFDFF\uFE70-\uFEFF]+$");
                if (!regex.IsMatch(userName))
                {
                    errors.Add(new IdentityError
                    {
                        Code = "InvalidUserName",
                        Description = "نام کاربری فقط می‌تواند شامل حروف فارسی، انگلیسی، اعداد و کاراکترهای _ - @ . و فاصله باشد."
                    });
                }
            }

            return errors.Count == 0 ? IdentityResult.Success : IdentityResult.Failed(errors.ToArray());
        }
    }
}