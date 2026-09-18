using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SugarShop.Domain.Entities;
using SugarShop.Domain.Entities.Sales;
using SugarShop.Infrastructure.Persistence;
using SugarShop.Infrastructure.Persistence.Sales;
using SugarShop.Web.ViewModels;

namespace SugarShop.Web.ViewComponents
{
    /// <summary>یک سفارش اخیر مشتری برای کارت «سفارش‌های من» در صفحه اصلی اپ.</summary>
    public class AppHomeOrderVm
    {
        public int Id { get; set; }
        public string OrderCode { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public OrderStatus Status { get; set; }
        public PaymentStatus PaymentStatus { get; set; }

        /// <summary>اگر مبلغی برای پرداخت باز باشد، دکمه پرداخت مستقیم نشان داده می‌شود.</summary>
        public bool IsPaymentEnabled { get; set; }

        public int ItemCount { get; set; }
    }

    /// <summary>یک شیرینی تازه برای ردیف پیشنهادها.</summary>
    public class AppHomeSweetVm
    {
        public int Id { get; set; }
        public string TitleFa { get; set; } = "";
        public string? ImagePath { get; set; }
        public decimal PricePerKg { get; set; }
        public int ApproxWeightGrams { get; set; }
        public bool InStock { get; set; }
    }

    public class AppHomeModel
    {
        public string? CustomerName { get; set; }
        public bool IsSignedIn { get; set; }
        public List<CategoryItemVm> Categories { get; set; } = new();
        public List<AppHomeOrderVm> RecentOrders { get; set; } = new();
        public List<AppHomeSweetVm> FreshSweets { get; set; } = new();
    }

    /// <summary>
    /// بلوک‌های ابتدای صفحه اصلی در حالت «داخل اپ»: سلام و احوال‌پرسی، میان‌بر دسته‌بندی‌ها،
    /// آخرین سفارش‌ها و پیشنهاد شیرینی تازه.
    ///
    /// چرا ViewComponent؟ چون داده‌اش (سفارش‌های خودِ مشتری و تازه‌های کاتالوگ) در کنترلر خانه
    /// نیست و اینجا در یک جا و به‌صورت async خوانده می‌شود. دسته‌بندی‌ها از خود صفحه اصلی پاس
    /// داده می‌شوند تا کوئری تکراری نزنیم.
    ///
    /// این بلوک فقط برای کلاینت‌های داخل اپ رندر می‌شود (کنترل آن در صفحه اصلی انجام می‌شود)،
    /// پس بازدیدکننده‌های وب هیچ کوئری اضافه‌ای بابتش نمی‌دهند.
    /// </summary>
    public class AppHomeViewComponent : ViewComponent
    {
        private readonly SugarShopSalesDbContext _salesDb;
        private readonly SugarShopCatalogDbContext _catalogDb;
        private readonly UserManager<ApplicationUser> _userManager;

        public AppHomeViewComponent(
            SugarShopSalesDbContext salesDb,
            SugarShopCatalogDbContext catalogDb,
            UserManager<ApplicationUser> userManager)
        {
            _salesDb = salesDb;
            _catalogDb = catalogDb;
            _userManager = userManager;
        }

        public async Task<IViewComponentResult> InvokeAsync(
            List<CategoryItemVm>? categories = null,
            int sweetCount = 8,
            int orderCount = 3)
        {
            var model = new AppHomeModel
            {
                Categories = (categories ?? new List<CategoryItemVm>()).Where(c => !string.IsNullOrWhiteSpace(c.TitleFa)).ToList()
            };

            // در ViewComponent، کاربر جاری از UserClaimsPrincipal خوانده می‌شود
            var principal = UserClaimsPrincipal;
            var user = principal?.Identity?.IsAuthenticated == true
                ? await _userManager.GetUserAsync(principal)
                : null;
            model.IsSignedIn = user != null;
            model.CustomerName = user?.FullName?.Trim().Split(' ').FirstOrDefault();

            if (user != null)
            {
                // همان فیلتر صفحه «سفارش‌های من»: شارژ کیف پول و سفارش موقت کیک سفارشی جزو سفارش‌ها نیستند
                var orders = await _salesDb.Orders.AsNoTracking()
                    .Where(o => o.UserId == user.Id
                        && o.Notes != "WalletRecharge"
                        && (o.Notes == null || !o.Notes.StartsWith("CustomCakeOrder_")))
                    .OrderByDescending(o => o.CreatedAt)
                    .Take(orderCount)
                    .Select(o => new AppHomeOrderVm
                    {
                        Id = o.Id,
                        OrderCode = o.OrderCode,
                        CreatedAt = o.CreatedAt,
                        Status = o.OrderStatus,
                        PaymentStatus = o.PaymentStatus,
                        IsPaymentEnabled = o.IsPaymentEnabled,
                        ItemCount = o.Items.Count
                    })
                    .ToListAsync();

                model.RecentOrders = orders;
            }

            // «تازه‌ها»: آخرین شیرینی‌های فعالی که موجودی دارند (قدیمی‌ترها پایین‌تر می‌مانند)
            model.FreshSweets = await _catalogDb.SweetItems.AsNoTracking()
                .Where(s => s.IsActive)
                .OrderByDescending(s => s.CreatedAt)
                .ThenByDescending(s => s.Id)
                .Take(sweetCount)
                .Select(s => new AppHomeSweetVm
                {
                    Id = s.Id,
                    TitleFa = s.TitleFa,
                    ImagePath = s.ImagePath,
                    PricePerKg = s.PricePerKg,
                    ApproxWeightGrams = s.ApproxWeightGrams,
                    InStock = s.InventoryCount > 0
                })
                .ToListAsync();

            return View(model);
        }
    }
}
