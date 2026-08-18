using SugarShop.Domain.Entities.Sales;
using System.ComponentModel.DataAnnotations;

namespace SugarShop.Web.ViewModels
{
    public class CheckoutViewModel
    {
        public List<Address>? UserAddresses { get; set; }
        public int? SelectedAddressId { get; set; }
        [Display(Name = "عنوان (خانه، محل کار)")]
        public string? NewAddressTitle { get; set; }

        [Display(Name = "آدرس کامل")]
        public string? NewFullAddress { get; set; }

        [Display(Name = "کد پستی")]
        public string? NewPostalCode { get; set; }

        [Display(Name = "نام گیرنده")]
        public string? NewReceiverName { get; set; }

        [Display(Name = "شماره تماس")]
        public string? NewReceiverPhone { get; set; }
        [Display(Name = "تاریخ تحویل (اختیاری)")]
        public DateTime? DeliveryDate { get; set; }

        [Display(Name = "ساعت تحویل (اختیاری)")]
        public TimeSpan? DeliveryTime { get; set; }

        [Display(Name = "توضیحات (اختیاری)")]
        public string? Notes { get; set; }
        public bool UseWallet { get; set; }
        public string? GuestFullAddress { get; set; }
        public string? GuestPostalCode { get; set; }
        public string? GuestReceiverName { get; set; }
        public string? GuestReceiverPhone { get; set; }

        public bool UseNewAddress { get; set; }

    }
}