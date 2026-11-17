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

        // 导航属性（后续添加 Merchant 和 CustomerCard 实体后取消注释）
        public Merchant? MerchantProfile { get; set; }
        // public ICollection<CustomerCard> CustomerCards { get; set; } = new List<CustomerCard>();
    }
}
