using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SugarShop.Domain.Entities
{
    public class BoxType
    {
        public int Id { get; set; }
        public string TitleFa { get; set; } = "";
        public int CapacityGrams { get; set; }
        public int MaxRows { get; set; }
        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
