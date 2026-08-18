using System;
using System.Collections.Generic;
using System.Linq;

namespace SugarShop.Web.SessionModels
{
    public class CartItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public DateTime AddedAt { get; set; } = DateTime.Now;
        public int BoxTypeId { get; set; }
        public string BoxTitle { get; set; } = "";
        public int BoxCapacityGrams { get; set; }
        public int BoxMaxRows { get; set; }
        public List<BoxSelectionSessionItem> Items { get; set; } = new();
        public int TotalRows => Items.Count;
        public int TotalApproxWeight => BoxCapacityGrams;
        public decimal TotalApproxPrice => Items.Sum(item => (item.PricePerKg * item.ApproxWeightGrams) / 1000m);
    }
    public class CartProductItem
    {
        public int ProductId { get; set; }
        public string Title { get; set; } = "";
        public decimal Price { get; set; }
        public int WeightGrams { get; set; }
        public int Quantity { get; set; }
        public decimal TotalPrice => Price * Quantity;
    }
    public class CartSessionState
    {
        public const string SessionKey = "CART_SESSION_STATE";
        public List<CartItem> Items { get; set; } = new();
        public List<CartProductItem> Products { get; set; } = new();
        public string? AppliedDiscountCode { get; set; }
        public int? AppliedDiscountCodeId { get; set; }
        public decimal? DiscountAmount { get; set; }
        public int TotalItems => Items.Count + Products.Count;
        public int TotalProductItems => Products.Sum(p => p.Quantity);
        public decimal TotalProductPrice => Products.Sum(p => p.TotalPrice);
        public int TotalProductWeight => Products.Sum(p => p.WeightGrams * p.Quantity);
        public decimal TotalApproxPrice =>
            Items.Sum(x => x.TotalApproxPrice) + TotalProductPrice - (DiscountAmount ?? 0);
        public int TotalApproxWeight =>
            Items.Sum(x => x.BoxCapacityGrams) + TotalProductWeight;
        public void Clear()
        {
            Items.Clear();
            Products.Clear();
            AppliedDiscountCode = null;
            AppliedDiscountCodeId = null;
            DiscountAmount = null;
        }
        public void RemoveItem(string id)
        {
            Items.RemoveAll(x => x.Id == id);
        }
        public void RemoveProduct(int productId)
        {
            Products.RemoveAll(p => p.ProductId == productId);
        }
    }
    public static class CartSessionExtensions
    {
        public static void SetCartSessionState(this ISession session, CartSessionState state)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(state);
            session.SetString(CartSessionState.SessionKey, json);
        }

        public static CartSessionState GetCartSessionState(this ISession session)
        {
            var json = session.GetString(CartSessionState.SessionKey);
            return string.IsNullOrWhiteSpace(json)
                ? new CartSessionState()
                : System.Text.Json.JsonSerializer.Deserialize<CartSessionState>(json) ?? new CartSessionState();
        }
    }
}