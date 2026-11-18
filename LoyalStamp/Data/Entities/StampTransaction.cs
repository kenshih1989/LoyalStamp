using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LoyalStamp.Data.Entities;

public class StampTransaction
{
    [Key]
    public int Id { get; set; }

    [Required]
    public Guid CustomerCardId { get; set; }

    [Required]
    [MaxLength(20)]
    public string TransactionType { get; set; } = string.Empty; // "Earn" or "Redeem"

    [Required]
    public int StampsChanged { get; set; } // Positive for Earn, negative for Redeem

    [Column(TypeName = "decimal(18,2)")]
    public decimal? SpendAmount { get; set; } // Populated when TransactionType == "Earn"

    [MaxLength(200)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // Navigation property
    [ForeignKey(nameof(CustomerCardId))]
    public CustomerCard CustomerCard { get; set; } = null!;
}