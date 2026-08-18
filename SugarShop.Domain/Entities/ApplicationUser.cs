using Microsoft.AspNetCore.Identity;

namespace SugarShop.Domain.Entities
{
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; } = "";
        public string? AvatarPath { get; set; }
        public DateTime? BirthDate { get; set; }
        public string? Gender { get; set; }

    }
}