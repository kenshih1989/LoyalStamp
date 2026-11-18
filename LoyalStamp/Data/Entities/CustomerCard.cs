using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LoyalStamp.Data.Entities;

public class CustomerCard
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(12)]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public int CampaignId { get; set; }

    [Required]
    [MaxLength(12)]
    public string CardCode { get; set; } = string.Empty; // Example: LS-8A92B

    public int CurrentStamps { get; set; } = 0;

    public int TotalRedeemedCount { get; set; } = 0;

    public DateTime LastUpdated { get; set; } = DateTime.Now;

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    // navigation properties
    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;

    [ForeignKey(nameof(CampaignId))]
    public StampCampaign Campaign { get; set; } = null!;

    // navigation properties: uncomment after added the StampTransaction entity
    // public ICollection<StampTransaction> Transactions { get; set; } = new List<StampTransaction>();
}