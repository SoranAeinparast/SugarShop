using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SugarShop.Domain.Entities.Newsletter;
using SugarShop.Infrastructure.Persistence.Sales;
using System.Net;
using System.Net.Mail;

namespace SugarShop.Web.Controllers.Admin
{
    [Authorize(Roles = "Admin,Owner")]
    [Route("Admin/Newsletter")]
    public class NewsletterController : Controller
    {
        private readonly SugarShopSalesDbContext _context;

        public NewsletterController(SugarShopSalesDbContext context)
        {
            _context = context;
        }
        [HttpGet]
        public IActionResult Index()
        {
            return RedirectToAction(nameof(Subscribers));
        }

        [HttpGet("Subscribers")]
        public async Task<IActionResult> Subscribers()
        {
            var subscribers = await _context.Subscribers.OrderByDescending(s => s.CreatedAt).ToListAsync();
            return View(subscribers);
        }

        [HttpPost("DeleteSubscriber")]
        public async Task<IActionResult> DeleteSubscriber(int id)
        {
            var sub = await _context.Subscribers.FindAsync(id);
            if (sub != null)
            {
                _context.Subscribers.Remove(sub);
                await _context.SaveChangesAsync();
                TempData["Success"] = "اشتراک حذف شد.";
            }
            return RedirectToAction(nameof(Subscribers));
        }

        [HttpGet("Settings")]
        public async Task<IActionResult> Settings()
        {
            var setting = await _context.NewsletterSettings.FirstOrDefaultAsync();
            if (setting == null)
            {
                setting = new NewsletterSetting();
                _context.NewsletterSettings.Add(setting);
                await _context.SaveChangesAsync();
            }
            return View(setting);
        }

        [HttpPost("Settings")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Settings(NewsletterSetting model)
        {
            if (ModelState.IsValid)
            {
                var setting = await _context.NewsletterSettings.FirstOrDefaultAsync();
                if (setting != null)
                {
                    setting.SmtpHost = model.SmtpHost;
                    setting.SmtpPort = model.SmtpPort;
                    setting.EnableSsl = model.EnableSsl;
                    setting.Username = model.Username;
                    setting.Password = model.Password;
                    setting.FromEmail = model.FromEmail;
                    setting.FromName = model.FromName;
                    setting.IsActive = model.IsActive;
                    setting.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    _context.NewsletterSettings.Add(model);
                }
                await _context.SaveChangesAsync();
                TempData["Success"] = "تنظیمات ذخیره شد.";
                return RedirectToAction(nameof(Settings));
            }
            return View(model);
        }

        [HttpGet("Send")]
        public IActionResult Send()
        {
            return View();
        }

        [HttpPost("Send")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Send(string subject, string body)
        {
            if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(body))
            {
                TempData["Error"] = "موضوع و متن خبرنامه الزامی است.";
                return View();
            }

            var setting = await _context.NewsletterSettings.FirstOrDefaultAsync();
            if (setting == null || !setting.IsActive || string.IsNullOrEmpty(setting.SmtpHost))
            {
                TempData["Error"] = "تنظیمات SMTP پیکربندی نشده است.";
                return View();
            }

            var subscribers = await _context.Subscribers.Where(s => s.IsActive).ToListAsync();
            if (!subscribers.Any())
            {
                TempData["Error"] = "هیچ مشترکی وجود ندارد.";
                return View();
            }

            _ = Task.Run(() => SendBulkEmail(subscribers, subject, body, setting));
            TempData["Success"] = $"ارسال خبرنامه به {subscribers.Count} نفر در پس‌زمینه آغاز شد.";
            return RedirectToAction(nameof(Subscribers));
        }

        private void SendBulkEmail(List<Subscriber> subscribers, string subject, string body, NewsletterSetting setting)
        {
            using var client = new SmtpClient(setting.SmtpHost, setting.SmtpPort);
            client.EnableSsl = setting.EnableSsl;
            client.Credentials = new NetworkCredential(setting.Username, setting.Password);

            var fromAddress = new MailAddress(setting.FromEmail, setting.FromName);
            foreach (var sub in subscribers)
            {
                try
                {
                    var mail = new MailMessage(fromAddress, new MailAddress(sub.Email));
                    mail.Subject = subject;
                    mail.Body = body + $"\n\nبرای لغو اشتراک روی لینک زیر کلیک کنید:\n{GetUnsubscribeLink(sub)}";
                    mail.IsBodyHtml = false;
                    client.Send(mail);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to send to {sub.Email}: {ex.Message}");
                }
            }
        }

        private string GetUnsubscribeLink(Subscriber sub)
        {
            var token = sub.UnsubscribeToken ?? Guid.NewGuid().ToString();
            if (sub.UnsubscribeToken == null)
            {
                sub.UnsubscribeToken = token;
                _context.SaveChanges();
            }
            return $"{Request.Scheme}://{Request.Host}/Newsletter/Unsubscribe?token={token}";
        }
    }
}