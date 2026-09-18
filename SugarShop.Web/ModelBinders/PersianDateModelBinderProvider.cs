using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace SugarShop.Web.ModelBinders
{
    public class PersianDateModelBinderProvider : IModelBinderProvider
    {
        public IModelBinder GetBinder(ModelBinderProviderContext context)
        {
            var modelType = context.Metadata.ModelType;
            if (modelType == typeof(DateTime) || modelType == typeof(DateTime?))
                return new PersianDateModelBinder();

            return null!; // returning null is the contract: "no binder for this type"
        }
    }
}