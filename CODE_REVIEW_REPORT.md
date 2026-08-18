# گزارش بررسی کد پروژه SugarShop

تاریخ بررسی: ۱۴۰۵/۰۵/۲۶

---

## 🔴 مشکلات بحرانی (امنیت)

### ۱. لو رفتن کلیدها و رمزهای عبور در فایل‌های پیکربندی
**فایل:** `SugarShop.Web/appsettings.json`

تمام کلیدهای API و رمزهای عبور به‌صورت متن ساده در فایل پیکربندی ذخیره شده‌اند:

| مورد | مقدار لو رفته |
|------|---------------|
| Connection String (رمز SA) | `Soran@1357` |
| رمز SMTP | `Soran@1357@1978` |
| OpenRouter API Key | `sk-or-v1-f04e0594...` |
| AgnesAI API Key | `sk-xLYwj9ot...` |
| Unsplash Access Key | `PPyGBA3GQFk...` |
| Zarinpal MerchantId (fallback) | `cfa83c81-89b0-...` |

**توصیه:** کلیدها باید از User Secrets یا متغیرهای محیطی خوانده شوند. فایل‌های `appsettings.json` و `appsettings.Development.json` نباید در Git commit شوند (به `.gitignore` اضافه شوند). تمام کلیدهای لو رفته باید فوراً باطل و جایگزین شوند.

---

### ۲. رمزهای عبور硬کد شده در Program.cs
**فایل:** `SugarShop.Web/Program.cs` (سطر ۱۷۵، ۱۸۵، ۱۹۲)

```csharp
await userManager.CreateAsync(ownerUser, "Soran@1357@1978#S61_R88&E90@1405");
await userManager.CreateAsync(adminUser, "Admin@123");
await userManager.CreateAsync(managerUser, "Manager@123");
```

رمزهای عبور کاربران پیش‌فرض (Owner، Admin، Manager) مستقیماً در کد نوشته شده‌اند. هر کسی که به کد دسترسی داشته باشد می‌تواند وارد سیستم شود.

**توصیه:** رمزها از متغیرهای محیطی یا User Secrets خوانده شوند. یا بهتر: کاربر Owner فقط در محیط Development ساخته شود و در Production از طریق فرم Setup اولیه ایجاد شود.

---

### ۳. کنترلر Setup بدون احراز هویت
**فایل:** `SugarShop.Web/Controllers/SetupController.cs`

کنترلر `SetupController` هیچ ویژگی `[Authorize]` ندارد. هر کسی با مراجعه به آدرس `/Setup/CreateAdmin` می‌تواند:
- حساب admin قبلی را حذف کند
- حساب admin جدید با رمز شناخته‌شده `Admin@123` بسازد
- نقش Admin را به آن اختصاص دهد

این یک در پشتی کامل برای هک سیستم است.

**توصیه:** این کنترلر باید کامل حذف شود یا حداقل با `[Authorize(Roles = "Owner")]` محدود شود و فقط در محیط Development در دسترس باشد.

---

### ۴. دسترسی عمومی به داشبورد Hangfire
**فایل:** `SugarShop.Web/Program.cs` (سطر ۲۴۵-۲۵۱)

```csharp
public bool Authorize(DashboardContext context)
{
    return httpContext.User.Identity?.IsAuthenticated == true;
}
```

فیلتر احراز هویت داشبورد Hangfire فقط بررسی می‌کند که کاربر «وارد شده» باشد — نه اینکه Admin باشد. هر کاربر عادی می‌تواند به `/hangfire` دسترسی پیدا کند و jobهای پس‌زمینه را ببیند/کنترل کند.

**توصیه:**
```csharp
return httpContext.User.IsInRole("Admin") || httpContext.User.IsInRole("Owner");
```

---

### ۵. کنترلرهای تست AI بدون احراز هویت
**فایل‌ها:**
- `SugarShop.Web/Controllers/TestDeepSeekController.cs`
- `SugarShop.Web/Controllers/TestGeminiController.cs`
- `SugarShop.Web/Controllers/TestHuggingFaceController.cs`

این سه کنترلر هیچ `[Authorize]` ندارند و هر کاربر ناشناس می‌تواند با مراجعه به `/TestDeepSeek`، `/TestGemini`، `/TestHuggingFace` درخواست به APIهای AI ارسال کند که باعث مصرف اعتبار (Credit) و هزینه می‌شود.

**توصیه:** این کنترلرها باید حذف شوند یا حداقل با `[Authorize(Roles = "Admin,Owner")]` محدود شوند و فقط در محیط Development در دسترس باشند.

---

### ۶. نبود Idempotency در Callback پرداخت — امکان شارژ مضاعف کیف پول
**فایل:** `SugarShop.Web/Controllers/PaymentController.cs` (سطر ۹۸-۱۷۶)

اکشن `Callback` یک متد `GET` است که در آن:
- وضعیت سفارش و پرداخت تغییر می‌کند
- موجودی کیف پول افزایش می‌یابد
- موجودی انبار کاهش می‌یابد

هیچ بررسی Idempotency وجود ندارد. اگر کاربر (یا مهاجم) URL کال‌بک را چند بار بازخوانی کند، هیچ مانعی برای پردازش مجدد وجود ندارد. به‌علاوه، در Verify پرداخت، مبلغ از پاسخ درگاه تأیید نمی‌شود — فقط `trackId` بررسی می‌شود.

**توصیه:**
- قبل از پردازش، بررسی شود `payment.PaymentStatus != PaymentStatus.Succeeded`
- مبلغ پرداخت از پاسخ درگاه استخراج و با مبلغ سفارش مقایسه شود
- پردازش در یک Transaction انجام شود

---

## 🟠 مشکلات با اهمیت بالا

### ۷. کسر مضاعف موجودی انبار
**فایل‌ها:**
- `SugarShop.Web/Controllers/CheckoutController.cs` (سطر ۲۱۷)
- `SugarShop.Web/Controllers/PaymentController.cs` (سطر ۱۳۳)
- `SugarShop.Web/Controllers/AdminController.cs` (سطر ۳۹۵)

در `CheckoutController.PlaceOrder`، موجودی محصول مستقیماً کم می‌شود:
```csharp
product.Inventory -= productItem.Quantity;
```

سپس در `PaymentController.Callback` بعد از پرداخت موفق:
```csharp
await _inventoryService.DecreaseInventoryAsync(order);
```

و در `AdminController.UpdateOrderStatus` اگر وضعیت به `Paid` تغییر کند:
```csharp
if (status == OrderStatus.Paid && oldStatus != OrderStatus.Paid)
    await _inventoryService.DecreaseInventoryAsync(order);
```

موجودی محصول می‌تواند تا ۳ بار کسر شود.

**توصیه:** کسر موجودی فقط در یک نقطه انجام شود (ترجیحاً بعد از پرداخت موفق) و یک فلگ `InventoryDeducted` روی Order اضافه شود تا جلوی کسر مجدد گرفته شود.

---

### ۸. نبود Transaction در ثبت سفارش
**فایل:** `SugarShop.Web/Controllers/CheckoutController.cs` (سطر ۷۰-۲۵۵)

در اکشن `PlaceOrder`، چندین بار `SaveChangesAsync` روی دو DbContext مختلف (Catalog و Sales) فراخوانی می‌شود:
1. ذخیره آدرس (Sales)
2. ذخیره سفارش (Sales)
3. کسر موجودی محصولات (Catalog)
4. ذخیره آیتم‌های سفارش (Sales)
5. به‌روزرسانی تراکنش کیف پول (Sales)
6. به‌روزرسانی کد تخفیف (Sales)

اگر هر کدام از این مراحل شکست بخورد، داده‌ها در حالت ناسازگار می‌مانند. مثلاً موجودی محصول کم شده ولی سفارش ذخیره نشده، یا کیف پول کاربر کم شده ولی سفارش کامل نشده.

**توصیه:** کل عملیات در یک `TransactionScope` یا `IDbContextTransaction` پیچیده شود.

---

### ۹. نبود CSRF Protection روی اکشن‌های حساس
**فایل‌های آسیب‌پذیر:**
- `CartController.cs` — تمام POST اکشن‌ها (AddProductToCart، RemoveProduct، RemoveItem، ClearCart، ApplyDiscount، RemoveDiscount)
- `CheckoutController.cs` — اکشن PlaceOrder
- `NewsletterController.cs` — اکشن Subscribe

این اکشن‌ها `[ValidateAntiForgeryToken]` ندارند و در برابر حملات CSRF آسیب‌پذیرند.

**توصیه:** به تمام POST اکشن‌های state-mutating ویژگی `[ValidateAntiForgeryToken]` اضافه شود.

---

### ۱۰. متدهای GET که وضعیت سیستم را تغییر می‌دهند
**فایل‌های آسیب‌پذیر:**
- `PaymentController.Callback` — پردازش پرداخت و شارژ کیف پول (GET)
- `BoxController.Status` — اضافه کردن شیرینی به جعبه (GET)
- `NewsletterController.Unsubscribe` — لغو اشتراک (GET)
- `ProductsController` — اکشن‌های GET که SaveChanges فراخوانی می‌کنند
- `AddressController` — اکشن‌های GET که داده ذخیره می‌کنند

متدهای GET نباید state تغییر دهند چون در برابر pre-fetch، image tags و CSRF آسیب‌پذیرند.

**توصیه:** تمام اکشن‌های تغییر state به POST تبدیل شوند.

---

### ۱۱. عدم اعتبارسنجی فایل‌های آپلودی
**فایل‌های آسیب‌پذیر:**
- `AdminController.cs` (SaveFile)
- `CustomCakeOrderController.cs` (SaveFile)
- `ProfileController.cs` (EditProfile)
- `CategoriesController.cs` (Create/Edit)
- `SweetItemsController.cs`
- `BoxTypesController.cs`
- `ProductsController.cs`

هیچکدام از این متدها نوع، حجم یا پسوند فایل را بررسی نمی‌کنند. کاربر می‌تواند:
- فایل `.exe`، `.aspx`، یا `.html` آپلود کند
- فایل با حجم بسیار بزرگ آپلود کند و دیسک را پر کند
- فایل با نام مخرب آپلود کند

**توصیه:** پسوند فایل (Allow-list)، MIME type، حجم حداکثر (مثلاً ۵MB) و ترجیحاً file signature بررسی شود. در `selectedAvatar` (ProfileController) مسیر فایل باید اعتبارسنجی شود تا کاربر نتواند مسار دلخواه تنظیم کند.

---

### ۱۲. قیمت‌ها از Session خوانده می‌شوند نه از دیتابیس
**فایل:** `SugarShop.Web/Controllers/CheckoutController.cs`

در `PlaceOrder`، قیمت محصولات و شیرینی‌ها از Session (که موقع Add to Cart ذخیره شده) خوانده می‌شود، نه از دیتابیس. کاربر می‌تواند قیمت‌ها را در Session دستکاری کند و با قیمت صفر سفارش ثبت کند.

**توصیه:** در زمان ثبت سفارش، قیمت‌ها باید از دیتابیس مجدداً خوانده و محاسبه شوند.

---

### ۱۳. پذیرش محصول ناموجود در سبد خرید
**فایل:** `SugarShop.Web/Controllers/CartController.cs` (سطر ۵۰-۵۹)

```csharp
if (isOutOfStock)
{
    return Json(new { success = true, ... message = "موجودی به اتمام رسیده است..." });
}
```

وقتی محصول ناموجود است، آن را با موفقیت به سبد اضافه می‌کند و فقط پیام هشدار می‌دهد. کاربر می‌تواند سفارش محصول ناموجود ثبت کند.

---

## 🟡 مشکلات متوسط

### ۱۴. کد Merchant در ZibalPaymentService هاردکد شده
**فایل:** `SugarShop.Infrastructure/Services/ZibalPaymentService.cs` (سطر ۱۸)

```csharp
_merchant = "zibal";
```

مقدار merchant به‌صورت ثابت `"zibal"` تنظیم شده و از IConfiguration خوانده نمی‌شود. این یعنی از حالت Sandbox یا کلید اختصاصی پشتیبانی نمی‌کند.

---

### ۱۵. خروجی HTML به جای Excel واقعی
**فایل:** `SugarShop.Web/Controllers/AdminController.cs` (سطر ۱۶۸-۲۳۱)

اکشن `ExportOrdersToExcel` یک فایل HTML با پسوند `.xls` تولید می‌کند. این روش:
- فایل واقعی Excel نیست
- آسیب‌پذیر به HTML/Formula Injection است (داده کاربر بدون encoding در HTML قرار می‌گیرد)
- در Excel نسخه‌های جدید ممکن است هشدار امنیتی بدهد

**توصیه:** از کتابخانه‌ای مثل `ClosedXML` یا `EPPlus` برای تولید فایل Excel واقعی استفاده شود.

---

### ۱۶. استفاده از `new HttpClient()` به جای IHttpClientFactory
**فایل‌ها:**
- `SugarShop.Web/Services/Implementations/AIContentService.cs` (سطر ۳۷)
- `SugarShop.Infrastructure/Services/ZibalPaymentService.cs` (تزریق HttpClient از HttpClientFactory استفاده می‌کند که درست است)

در `AIContentService`:
```csharp
_httpClient = new HttpClient();
```

استفاده مستقیم از `new HttpClient()` می‌تواند باعث exhausting سوکت‌ها (Socket Exhaustion) تحت بار زیاد شود.

---

### ۱۷. Console.WriteLine در کد Production
**فایل‌ها:**
- `SugarShop.Infrastructure/Services/ZibalPaymentService.cs` (سطر ۴۱-۴۳، ۹۲-۹۴)
- `SugarShop.Web/Controllers/WalletController.cs` (سطر ۲۲۲)

```csharp
Console.WriteLine("=== Zibal RequestPayment Response ===");
Console.WriteLine(responseString);
```

اطلاعات حساس (مثل پاسخ‌های درگاه پرداخت) در Console چاپ می‌شود. در Production این لاگ‌ها ممکن است به فایل‌های لاگ دسترسی‌پذیر برسند.

**توصیه:** از `ILogger<T>` استفاده شود.

---

### ۱۸. اکشن Settings در AdminController هیچ کاری نمی‌کند
**فایل:** `SugarShop.Web/Controllers/AdminController.cs` (سطر ۵۲۷-۵۳۳)

```csharp
public IActionResult Settings(int freeDeliveryThreshold, int walletCashbackPercent)
{
    TempData["SuccessMessage"] = "تنظیمات با موفقیت ذخیره شد.";
    return RedirectToAction("Settings");
}
```

پارامترها دریافت می‌شوند ولی هیچ‌جا ذخیره نمی‌شوند. کاربر پیام موفقیت می‌بیند ولی در واقع چیزی ذخیره نشده است.

---

### ۱۹. فعال نبودن تأیید ایمیل
**فایل:** `SugarShop.Web/Program.cs` (سطر ۷۸)

```csharp
options.SignIn.RequireConfirmedEmail = false;
```

کاربران می‌توانند بدون تأیید ایمیل ثبت‌نام و وارد شوند. این مشکل به‌خصوص در سیستم بازیابی رمز عبور که به ایمیل تکیه می‌کند، خطرناک است.

---

### ۲۰. فلگ `EnableSsl` در SMTP برابر `false` است
**فایل:** `SugarShop.Web/appsettings.json` (سطر ۲۸)

```json
"EnableSsl": false
```

با وجود اینکه پورت ۴۶۵ (SSL) استفاده می‌شود، SSL غیرفعال است. رمز عبور کاربران می‌تواند در شبکه قابل شنود باشد.

---

### ۲۱. ریسک SSRF در WebScraperService
**فایل:** `SugarShop.Web/Services/Implementations/WebScraperService.cs`

متد `FetchHtmlAsync(string url)` هر URL دریافتی را بدون محدودیت fetch می‌کند. مهاجم می‌تواند URLهای داخلی (`http://localhost:...`، `http://169.254.169.254/` برای metadata cloud) را ارسال کند تا به سرویس‌های داخلی دسترسی پیدا کند.

**توصیه:** بررسی scheme (فقط http/https)، رد کردن آدرس‌های private/local، تنظیم timeout و محدودیت حجم پاسخ.

---

### ۲۲. XSS در نمایش محتوای AI
**فایل:** `SugarShop.Web/Views/Educational/Details.cshtml` (سطر ۳۹)

```cshtml
@Html.Raw(Model.BodyHtml)
```

محتوای تولیدشده توسط AI (یا اسکرپ شده) بدون encoding نمایش داده می‌شود. اگر محتوا حاوی تگ `<script>` باشد، منجر به XSS می‌شود.

**توصیه:** محتوا قبل از نمایش با HTML Sanitizer (مثل `Ganss.Xss.HtmlSanitizer`) پاک‌سازی شود.

---

## 🔵 مشکلات جزئی / کدهای تکراری

### ۲۳. ویژگی‌های تکراری در CartController
**فایل:** `SugarShop.Web/Controllers/CartController.cs` (سطر ۱۰۱-۱۰۴)

```csharp
[Authorize]
[HttpGet]
[Authorize]   // تکراری
[HttpGet]     // تکراری
public IActionResult Checkout()
```

ویژگی‌های `[Authorize]` و `[HttpGet]` دو بار نوشته شده‌اند.

---

### ۲۴. فایل‌های Class1.cs و کامنت‌های ✅ اضافی
**فایل‌ها:**
- `SugarShop.Application/Class1.cs` — فایل پیش‌فرض بدون استفاده
- `SugarShop.Domain/Class1.cs` — فایل پیش‌فرض بدون استفاده
- `SugarShop.Infrastructure/Class1.cs` — فایل پیش‌فرض بدون استفاده
- `SugarShop.Web/Services/EmailSender.cs` — کامنت‌های ✅ اضافی مثل `// ✅ این خط را حتماً اضافه کنید`

---

### ۲۵. N+1 Query در UsersController.Index
**فایل:** `SugarShop.Web/Controllers/UsersController.cs` (سطر ۳۸-۴۲)

```csharp
foreach (var user in users)
{
    var roles = await _userManager.GetRolesAsync(user);
    userRolesDict[user.Id] = roles.ToList();
}
```

برای هر کاربر یک کوئری مجزا به دیتابیس زده می‌شود. با تعداد زیاد کاربران این بسیار کند می‌شود.

---

### ۲۶. چند کوئری جداگانه برای نمودار داشبورد
**فایل:** `SugarShop.Web/Controllers/AdminController.cs` (سطر ۹۷-۱۰۸)

در حلقه ۷ روز اخیر، برای هر روز یک کوئری `SumAsync` جداگانه زده می‌شود. بهتر است با `GroupBy` در یک کوئری انجام شود.

---

### ۲۷. تکرار متد SaveFile در چند کنترلر
کد متد `SaveFile` در `AdminController`، `CustomCakeOrderController`، و `ProfileController` دقیقاً یکسان است. باید به یک Service مشترک منتقل شود.

---

### ۲۸. مشکل DI در SetupController
**فایل:** `SugarShop.Web/Controllers/SetupController.cs` (سطر ۱۱)

```csharp
private readonly UserManager<IdentityUser> _userManager;
```

این کنترلر `UserManager<IdentityUser>` تزریق می‌کند، در حالی که Identity برای `ApplicationUser` ثبت شده است. این کنترلر در Runtime با خطای DI مواجه خواهد شد.

---

## خلاصه اولویت‌بندی

| اولویت | تعداد | توضیح |
|--------|-------|-------|
| 🔴 بحرانی | ۶ | نشت کلیدها، در پشت امنیتی، دسترسی عمومی به قابلیت‌های حساس |
| 🟠 بالا | ۷ | باگ‌های مالی (کسر مضاعف، شارژ مضاعف)، CSRF، نبود Transaction |
| 🟡 متوسط | ۸ | کیفیت کد، XSS، SSRF، کانفیگ SMTP |
| 🔵 جزئی | ۷ | کد تکراری، N+1، فایل‌های بلااستفاده |

### توصیه‌های فوری
1. تمام کلیدهای لو رفته را فوراً باطل و جایگزین کنید
2. `appsettings.json` و `appsettings.Development.json` از Git حذف و به `.gitignore` اضافه شوند
3. `SetupController` و کنترلرهای `Test*` حذف یا محدود شوند
4. فیلتر Hangfire فقط به Admin/Owner محدود شود
5. بررسی Idempotency در Callback پرداخت اضافه شود
6. `[ValidateAntiForgeryToken]` به تمام POST اکشن‌ها اضافه شود
