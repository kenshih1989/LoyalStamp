using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace LoyalStamp.Data.Entities
{
    public class User : IdentityUser
    {
        [Required]
        [MaxLength(100)]
        public string FullName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // navigation properties
        public Merchant? MerchantProfile { get; set; }
        public ICollection<CustomerCard> CustomerCards { get; set; } = new List<CustomerCard>();
    }
}
