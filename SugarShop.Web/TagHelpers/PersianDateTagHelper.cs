using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using MD.PersianDateTime;

namespace SugarShop.Web.TagHelpers
{
    [HtmlTargetElement("persian-date")]
    public class PersianDateTagHelper : TagHelper
    {
        [HtmlAttributeName("asp-for")]
        public ModelExpression AspFor { get; set; }

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            output.TagName = "input";
            output.Attributes.SetAttribute("type", "text");
            output.Attributes.SetAttribute("class", "form-control persian-date");
            output.Attributes.SetAttribute("name", AspFor.Name);
            output.Attributes.SetAttribute("id", AspFor.Name);
            if (AspFor.Model != null && AspFor.Model is DateTime date && date > new DateTime(1900, 1, 1))
            {
                output.Attributes.SetAttribute("value", new PersianDateTime(date).ToString("yyyy/MM/dd"));
            }
            else
            {
                output.Attributes.SetAttribute("value", "");
            }
        }
    }
}