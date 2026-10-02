namespace SugarShop.Web.ViewModels
{
    public class UserWalletViewModel
    {
        public string UserId { get; set; } = null!;
        public string UserName { get; set; } = null!;
        public string FullName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public decimal Balance { get; set; }
    }
}