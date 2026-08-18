namespace SugarShop.Domain.Entities.Newsletter
{
    public class NewsletterSetting
    {
        public int Id { get; set; }
        public string SmtpHost { get; set; } = "";
        public int SmtpPort { get; set; } = 587;
        public bool EnableSsl { get; set; } = true;
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public string FromEmail { get; set; } = "";
        public string FromName { get; set; } = "";
        public bool IsActive { get; set; } = true;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}