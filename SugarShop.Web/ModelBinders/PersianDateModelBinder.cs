using Microsoft.AspNetCore.Mvc.ModelBinding;
using MD.PersianDateTime;

namespace SugarShop.Web.ModelBinders
{
    public class PersianDateModelBinder : IModelBinder
    {
        public Task BindModelAsync(ModelBindingContext context)
        {
            var valueProviderResult = context.ValueProvider.GetValue(context.ModelName);
            if (valueProviderResult == ValueProviderResult.None)
                return Task.CompletedTask;

            var value = valueProviderResult.FirstValue;
            if (string.IsNullOrEmpty(value))
            {
                if (context.ModelType == typeof(DateTime?) || Nullable.GetUnderlyingType(context.ModelType) != null)
                {
                    context.Result = ModelBindingResult.Success(null);
                }
                return Task.CompletedTask;
            }

            try
            {
                DateTime date = PersianDateTime.Parse(value).ToDateTime();
                context.Result = ModelBindingResult.Success(date);
            }
            catch
            {
                context.ModelState.AddModelError(context.ModelName, "تاریخ نامعتبر است. فرمت صحیح: 1400/01/01");
            }

            return Task.CompletedTask;
        }
    }
}