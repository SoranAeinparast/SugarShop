# گزارش جامع اصلاحات پروژه SugarShop

## مقدمه

این گزارش تمام تغییرات و اصلاحات انجام‌شده در پروژه SoranSoft_SugarShop را در ۵ فاز اصلی پوشش می‌دهد.

**نکته مهم:** به دلیل عدم وجود .NET SDK در محیط آزمایشی، تغییرات بیلد نشده‌اند. شما باید تغییرات را به‌صورت محلی بیلد و تست کنید.

---

## فاز ۱: اصلاحات امنیتی و مالی

### ۱.۱ پاکسازی رازها (Secrets)
- **`appsettings.json`**: تمام کلیدهای API و رمزهای عبور پاک‌سازی شدند. بخش‌های `Zibal`، `SeedPasswords` و `EnableSsl` اضافه شد.
- **`appsettings.Development.json`**: تمام رازها پاک‌سازی شدند.
- **هشدار**: کلیدهای API قبلی (OpenRouter، AgnesAI، Unsplash، رمز SA، رمز SMTP) اکنون در معرض عموم هستند و باید فوراً چرخش (rotate) شوند.

### ۱.۲ امنیت Program.cs
- رمزهای عبور ادمین و Owner از Configuration خوانده می‌شوند (نه hardcoded)
- Hangfire Dashboard فقط برای نقش‌های Admin و Owner در دسترس است
- `CookieSecurePolicy.Always` فعال شد
- `AddHttpClient()` و `AddHttpClient<ZibalPaymentService>()` ثبت شدند

### ۱.۳ حذف کنترلرهای ناامن
- `SetupController.cs` — حذف شد (ایجاد ادمین بدون محدودیت)
- `TestDeepSeekController.cs` — حذف شد (آزمایش API بدون محدودیت)
- `TestGeminiController.cs` — حذف شد
- `TestHuggingFaceController.cs` — حذف شد

### ۱.۴ اصلاح PaymentController (Idempotency)
- **Idempotency**: اگر پرداخت قبلاً Succeeded شده، پردازش مجدد انجام نمی‌شود و کاربر redirect می‌شود
- **بررسی مبلغ**: تطابق مبلغ پرداخت با مبلغ سفارش قبل از تأیید
- **حذف کسر مضاعف موجودی**: موجودی فقط در `PaymentController.Callback` پس از پرداخت موفق کسر می‌شود (نه در `AdminController`)

### ۱.۵ اصلاح CheckoutController
- `[ValidateAntiForgeryToken]` به `PlaceOrder` اضافه شد
- `TransactionScope` برای عملیات اتمیک
- **محاسبه قیمت از دیتابیس**: قیمت‌ها از session استفاده نمی‌شوند، بلکه از DB خوانده می‌شوند
- **بررسی موجودی**: قبل از ثبت سفارش، موجودی محصولات بررسی می‌شود
- **حذف کسر مضاعف موجودی**: کسر موجودی از `PlaceOrder` حذف شد

### ۱.۶ اصلاح CartController
- `[ValidateAntiForgeryToken]` به تمام اکشن‌های POST اضافه شد
- تکرار attributes `[Authorize]` و `[HttpGet]` روی `Checkout()` حذف شد
- AJAX token به فراخوانی‌های `AddProductToCart` در `Product/Details.cshtml` و `Product/Index.cshtml` اضافه شد

### ۱.۷ اصلاح CSRF در سایر کنترلرها
- `NewsletterController.Subscribe`: `[ValidateAntiForgeryToken]` اضافه شد
- `NewsletterController.Unsubscribe`: به‌صورت GET باقی ماند (لینک‌های ایمیل)
- `ProfileController.DeleteOrder`, `PayOrder`, `Ticket`: `[ValidateAntiForgeryToken]` اضافه شد

### ۱.۸ اصلاح ZibalPaymentService
- Merchant ID از Configuration خوانده می‌شود
- `Console.WriteLine` با `ILogger` جایگزین شد

### ۱.۹ محافظت SSRF در WebScraperService
- اعتبارسنجی scheme (فقط http/https)
- مسدودسازی host‌های خطرناک (localhost، 169.254.169.254 و غیره)
- مسدودسازی IP‌های خصوصی (10.x، 172.16-31.x، 192.168.x، 169.254.x)
- timeout ۱۵ ثانیه
- محدودیت حجم پاسخ ۵ مگابایت
- استفاده از `IHttpClientFactory` به جای `new HttpClient()`

### ۱.۱۰ اصلاح AdminController
- `Settings` POST: `[ValidateAntiForgeryToken]` اضافه شد (هنوز به پیاده‌سازی پایداری نیاز دارد)
- حذف کسر مضاعف موجودی از `UpdateOrderStatus`

### ۱.۱۱ سایر
- `WalletController`: `Console.WriteLine` حذف شد
- فایل `.gitignore` اضافه شد (شامل appsettings و bin/obj)

---

## فاز ۲: اصلاح تولید محتوای AI

### ۲.۱ اصلاح AIContentService
- `new HttpClient()` با `IHttpClientFactory` جایگزین شد (جلوگیری از Socket Exhaustion)
- مدیریت خطا با `ILogger` اضافه شد
- timeout مناسب (۳۰ ثانیه برای تولید تصویر)
- fallback به placeholder در صورت خطا

### ۲.۲ اصلاح EducationalContentController
- اکشن `Create` (GET/POST) برای ایجاد دستی محتوا اضافه شد
- اکشن `GenerateAI` (POST) برای تولید محتوای AI از ورودی آزاد اضافه شد
- تزریق `IAIContentService` و `ILogger` به کنترلر

---

## فاز ۳: اصلاح مدیریت هدر دسته‌بندی

### ۳.۱ اصلاح خطای Razor در ویوی ادمین
- **خطای بحرانی**: در `CategoryHeaderSettings/Index.cshtml` کاراکتر اضافی `@` در `bgColorIn@put` باعث خطای کامپایل Razor می‌شد → اصلاح شد
- `section Scripts {` → `@section Scripts {` (کاهش `@`)

### ۳.۲ اصلاح mismatch مدل در CategoryHeaderSettingsController
- اکشن POST از `CategoryHeaderSettingsViewModel` به `CategoryHeaderSetting` (Entity مستقیم) تغییر کرد تا با `@model` ویو همخوانی داشته باشد
- import فضای نام `ViewModels` حذف شد

### ۳.۳ تأیید عملکرد صحیح
- `CategoryController.Index` تنظیمات هدر را از دیتابیس می‌خواند و به `ViewBag.HeaderSettings` ارسال می‌کند
- ویوی `Category/Index.cshtml` تنظیمات را به‌درستی نمایش می‌دهد (عنوان، زیرعنوان، رنگ پس‌زمینه، رنگ متن، ارتفاع، تصویر پس‌زمینه)

---

## فاز ۴: سیستم انتخاب از کتابخانه رسانه

### ۴.۱ ایجاد Partial مشترک
- **`_MediaPickerModal.cshtml`** در `Views/Shared/` ایجاد شد
- شامل: مودال کامل با جستجو، فیلتر نوع فایل، آپلود فایل جدید، pagination
- JavaScript کامل برای انتخاب فایل، preview و تأیید
- در `_AdminLayout` قرار داده شده (در همه صفحات ادمین در دسترس است)

### ۴.۲ اصلاح کنترلرها (۱۳ کنترلر)
تمام پارامترهای `IFormFile` از کنترلرهای زیر حذف شد و با فیلدهای مسیر (string) جایگزین شد:

| کنترلر | اکشن‌های اصلاح‌شده |
|--------|-------------------|
| CategoriesController | Create, Edit, Delete |
| ProductsController | Create, Edit, Delete |
| SweetItemsController | Create, Edit, Delete |
| SlidersController | Create, Edit, Delete |
| GalleryManagementController | Create, Edit, Delete |
| AdminController | EditCustomCakeOrder, DeleteCustomCakeOrder |
| CustomCakeOrderController | Create |
| SiteSettingsController (Controllers) | Index POST |
| ThemeController | SaveJson |
| SplashController | UploadVideo, DeleteVideo |
| AboutUsSettingsController | Index POST |
| SiteSettingsController (Admin) | Index POST |
| ProfileController | EditProfile, EditCustomCakeOrder, DeleteCustomCakeOrder |

**کدهای حذف‌شده:**
- تمام کدهای `Guid.NewGuid()`, `Path.Combine()`, `FileStream`, `CopyToAsync()`
- تمام کدهای `System.IO.File.Delete()` برای حذف فایل‌های قدیمی
- تمام `IWebHostEnvironment`‌های غیرضروری
- تمام متدهای کمکی `SaveFile`

### ۴.۳ اصلاح ویوها (۱۹ ویو)
تمام `<input type="file">` با الگوی زیر جایگزین شد:

```html
<input type="hidden" name="ImagePath" id="ImagePath" value="@Model.ImagePath" />
<img id="imagePreview" src="@Model.ImagePath" class="img-thumbnail mb-2" style="max-height:150px;" />
<button type="button" class="btn btn-outline-primary open-media-picker" data-target="#ImagePath" data-preview="#imagePreview">
    <i class="bi bi-image"></i> انتخاب از کتابخانه رسانه
</button>
```

تمام `enctype="multipart/form-data"` از فرم‌ها حذف شد.

### ۴.۴ اصلاح JavaScript
- `SiteSettings/Index.cshtml`: JS حذف اسلاید به‌روزرسانی شد (به جای `promoSliderFile` از `slidePreview` استفاده می‌کند)
- `Theme/Index.cshtml`: JS ذخیره‌سازی به‌روزرسانی شد (به جای `logoFile`/`faviconFile` از `LogoPath`/`FaviconPath` استفاده می‌کند)
- `Splash/Index.cshtml`: JS به‌روزرسانی شد (به جای `videoFile` از `VideoPath` استفاده می‌کند)
- `Product/Details.cshtml` و `Product/Index.cshtml`: CSRF token به AJAX calls اضافه شد

---

## فاز ۵: بهبود کیفیت کد

### ۵.۱ رفع N+1 در UsersController.Index
- به جای فراخوانی `GetRolesAsync` برای هر کاربر، یک کوئری واحد با `Join` روی `UserRoles` و `Roles` نوشته شد

### ۵.۲ حذف فایل‌های اضافی
- `Class1.cs` از پروژه‌های `Application`، `Domain` و `Infrastructure` حذف شد

### ۵.۳ اصلاح AIContentService
- `new HttpClient()` با `IHttpClientFactory` جایگزین شد
- مدیریت خطا و logging مناسب اضافه شد

### ۵.۴ اصلاح WebScraperService
- `new HttpClient()` با `IHttpClientFactory` جایگزین شد
- فضای نام `IWebScraperService` اضافه شد
- متد کمکی `IsPrivateOrLoopback` برای بررسی IP‌های خصوصی

---

## کارهای باقی‌مانده (نیاز به پیاده‌سازی توسط شما)

### ضروری:
1. **چرخش کلیدهای API**: تمام کلیدهای قدیمی (OpenRouter، AgnesAI، Unsplash، Zibal، SMTP، رمز SA) باید چرخش شوند و از طریق Environment Variables یا User Secrets پیکربندی شوند
2. **بیلد و تست محلی**: `dotnet restore && dotnet build` را اجرا کنید و خطاهای احتمالی را برطرف کنید
3. **پیاده‌سازی AdminController.Settings**: اکشن Settings هنوز تنظیمات را ذخیره نمی‌کند (فقط CSRF اضافه شد). نیاز به مدل و جدول تنظیمات اختصاصی

### توصیه‌شده:
4. **فعال‌سازی RequireConfirmedEmail**: فقط زمانی که ارسال ایمیل قابل اعتماد باشد
5. **بهینه‌سازی کوئری‌ها**: بررسی کوئری‌های دیگر برای N+1
6. **تست کامل**: لاگین، ادمین، کتابخانه رسانه، سبد خرید، checkout، callback پرداخت، تولید محتوای AI

---

## فهرست کامل فایل‌های تغییر‌یافته

### کنترلرها:
- `Controllers/PaymentController.cs` — بازنویسی کامل (idempotency)
- `Controllers/CheckoutController.cs` — بازنویسی کامل (Transaction، قیمت از DB)
- `Controllers/CartController.cs` — CSRF، حذف تکرار attributes
- `Controllers/NewsletterController.cs` — CSRF (Subscribe)، Unsubscribe به‌صورت GET باقی ماند
- `Controllers/ProfileController.cs` — CSRF، حذف IFormFile، حذف SaveFile
- `Controllers/AdminController.cs` — CSRF، حذف کسر مضاعف، حذف SaveFile
- `Controllers/WalletController.cs` — حذف Console.WriteLine
- `Controllers/UsersController.cs` — رفع N+1
- `Controllers/CategoriesController.cs` — حذف IFormFile
- `Controllers/ProductsController.cs` — حذف IFormFile
- `Controllers/SweetItemsController.cs` — حذف IFormFile
- `Controllers/SlidersController.cs` — حذف IFormFile
- `Controllers/CustomCakeOrderController.cs` — حذف IFormFile
- `Controllers/SiteSettingsController.cs` — حذف IFormFile
- `Controllers/Admin/GalleryManagementController.cs` — حذف IFormFile
- `Areas/Admin/Controllers/EducationalContentController.cs` — اضافه‌شدن Create و GenerateAI
- `Areas/Admin/Controllers/CategoryHeaderSettingsController.cs` — اصلاح مدل POST
- `Areas/Admin/Controllers/ThemeController.cs` — حذف IFormFile
- `Areas/Admin/Controllers/SplashController.cs` — حذف IFormFile
- `Areas/Admin/Controllers/AboutUsSettingsController.cs` — حذف IFormFile
- `Areas/Admin/Controllers/SiteSettingsController.cs` — حذف IFormFile

### سرویس‌ها:
- `Services/Implementations/AIContentService.cs` — IHttpClientFactory، مدیریت خطا
- `Services/Implementations/WebScraperService.cs` — SSRF protection، IHttpClientFactory

### ویوها:
- `Views/Shared/_MediaPickerModal.cshtml` — ایجاد شد
- `Views/Categories/Create.cshtml` و `Edit.cshtml`
- `Views/Products/Create.cshtml` و `Edit.cshtml`
- `Views/SweetItems/Create.cshtml` و `Edit.cshtml`
- `Views/Sliders/Create.cshtml` و `Edit.cshtml`
- `Views/SiteSettings/Index.cshtml`
- `Views/Profile/EditProfile.cshtml`، `EditCustomCakeOrder.cshtml`
- `Views/CustomCakeOrder/Create.cshtml`
- `Views/Admin/EditCustomCakeOrder.cshtml`
- `Views/Admin/GalleryManagement/Create.cshtml`، `Edit.cshtml`، `HeaderSettings.cshtml`
- `Views/Product/Details.cshtml` و `Index.cshtml` — CSRF token به AJAX
- `Areas/Admin/Views/CategoryHeaderSettings/Index.cshtml` — اصلاح خطای Razor
- `Areas/Admin/Views/Theme/Index.cshtml`
- `Areas/Admin/Views/Splash/Index.cshtml`
- `Areas/Admin/Views/AboutUsSettings/Index.cshtml`

### پیکربندی:
- `Program.cs` — بازنویشی امنیتی
- `appsettings.json` — پاک‌سازی رازها
- `appsettings.Development.json` — پاک‌سازی رازها
- `.gitignore` — اضافه شد

### حذف‌شده‌ها:
- `Controllers/SetupController.cs`
- `Controllers/TestDeepSeekController.cs`
- `Controllers/TestGeminiController.cs`
- `Controllers/TestHuggingFaceController.cs`
- `SugarShop.Application/Class1.cs`
- `SugarShop.Domain/Class1.cs`
- `SugarShop.Infrastructure/Class1.cs`
