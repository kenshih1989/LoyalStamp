using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LoyalStamp.Data.Entities;

public class StampCampaign
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int MerchantId { get; set; }

    [Required]
    [MaxLength(100)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal MinSpendAmount { get; set; }

    [Required]
    public int TargetStamps { get; set; }

    [Required]
    [MaxLength(150)]
    public string RewardItem { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // Navigation property
    [ForeignKey(nameof(MerchantId))]
    public Merchant Merchant { get; set; } = null!;

    public ICollection<CustomerCard> CustomerCards { get; set; } = new List<CustomerCard>();
}