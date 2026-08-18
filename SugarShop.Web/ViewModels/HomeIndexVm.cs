using SugarShop.Domain.Entities;
using System.Collections.Generic;

namespace SugarShop.Web.ViewModels
{
    public class HomeIndexVm
    {
        public List<CategoryItemVm> Categories { get; set; } = new();
        public List<SliderItemVm> Sliders { get; set; } = new();
        public SiteSetting? SiteSetting { get; set; }
    }
    public class CategoryItemVm
    {
        public int Id { get; set; }
        public string TitleFa { get; set; } = "";
        public string? ImagePath { get; set; }
        public string Slug { get; set; } = "";
    }
    public class SliderItemVm
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string? Subtitle { get; set; }
        public string? ImagePath { get; set; }
        public string? ButtonText { get; set; }
        public string? ButtonUrl { get; set; }
        public int SortOrder { get; set; }
        public bool IsVideo { get; set; } 
    }
}