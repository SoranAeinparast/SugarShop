using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;
using SugarShop.Domain.Entities.Newsletter; // ✅ این خط را حتماً اضافه کنید

namespace SugarShop.Web.Services
{
    public class EmailSender : IEmailSender
    {
        // ✅ تغییر از NewsletterSettings به NewsletterSetting
        private readonly NewsletterSetting _settings;
        private readonly ILogger<EmailSender> _logger;

        // ✅ تغییر از NewsletterSettings به NewsletterSetting در سازنده
        public EmailSender(IOptions<NewsletterSetting> settings, ILogger<EmailSender> logger)
        {
            _settings = settings.Value;
            _logger = logger;
        }

        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            try
            {
                if (!_settings.IsActive)
                {
                    _logger.LogWarning("سیستم ارسال ایمیل غیرفعال است.");
                    throw new Exception("سیستم ارسال ایمیل در حال حاضر غیرفعال است. لطفاً با پشتیبانی تماس بگیرید.");
                }

                using var client = new SmtpClient(_settings.SmtpHost, _settings.SmtpPort)
                {
                    Credentials = new NetworkCredential(_settings.Username, _settings.Password),
                    EnableSsl = _settings.EnableSsl,
                    Timeout = 10000
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(_settings.FromEmail, _settings.FromName, System.Text.Encoding.UTF8),
                    Subject = subject,
                    Body = htmlMessage,
                    IsBodyHtml = true,
                    SubjectEncoding = System.Text.Encoding.UTF8,
                    BodyEncoding = System.Text.Encoding.UTF8
                };
                mailMessage.To.Add(email);

                await client.SendMailAsync(mailMessage);
                _logger.LogInformation("ایمیل با موفقیت به {Email} ارسال شد.", email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در ارسال ایمیل به {Email}", email);
                throw;
            }
        }
    }
}