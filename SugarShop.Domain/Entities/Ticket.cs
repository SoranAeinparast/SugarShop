using System;

namespace SugarShop.Domain.Entities
{
    public enum TicketStatus
    {
        Open = 1,
        InProgress = 2,
        Answered = 3,
        Closed = 4
    }

    public class Ticket
    {
        public int Id { get; set; }
        public string UserId { get; set; } = "";
        public string Subject { get; set; } = "";
        public string Message { get; set; } = "";
        public string? AdminResponse { get; set; }
        public TicketStatus Status { get; set; } = TicketStatus.Open;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}