using System.Collections.Generic;
using System.Linq;

namespace SugarShop.Web.SessionModels
{
    public class BoxSelectionSessionItem
    {
        public int SweetItemId { get; set; }
        public string TitleFaSnapshot { get; set; } = "";
        public int ApproxWeightGrams { get; set; }
        public decimal PricePerKg { get; set; }
    }

    public class BoxSessionState
    {
        public const string SessionKey = "BOX_SESSION_STATE";

        public int? SelectedBoxTypeId { get; set; }
        public List<BoxSelectionSessionItem> Items { get; set; } = new();
        public int TotalRowsUsed => Items.Count;
        public int TotalApproxWeightGrams => Items.Sum(x => x.ApproxWeightGrams);
    }
}