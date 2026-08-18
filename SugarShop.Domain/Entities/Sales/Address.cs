using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SugarShop.Domain.Entities.Sales
{
    public class Address
    {
        public int Id { get; set; }

        public string UserId { get; set; } = "";
        public bool IsDefault { get; set; }
        public string Title { get; set; } = "";
        public string FullAddress { get; set; } = "";
        public string? PostalCode { get; set; }
        public string? ReceiverName { get; set; }
        public string? ReceiverPhone { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}