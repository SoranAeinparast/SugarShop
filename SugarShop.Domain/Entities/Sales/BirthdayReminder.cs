using System;

namespace SugarShop.Domain.Entities.Sales
{
    public class BirthdayReminder
    {
        public int Id { get; set; }
        public string? UserId { get; set; } 
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? Gender { get; set; }
        public DateTime BirthDate { get; set; }
        public string? Relation { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Notes { get; set; }
        public int RemindDaysBefore { get; set; } = 3;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}