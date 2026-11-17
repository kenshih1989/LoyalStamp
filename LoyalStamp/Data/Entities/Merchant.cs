using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LoyalStamp.Data.Entities;

public class Merchant
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(12)]
    public string UserId { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string StoreName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(300)]
    public string? LogoUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property to the User entity
    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;

    // Navigation property to the StampCampaign entity
    public ICollection<StampCampaign> Campaigns { get; set; } = new List<StampCampaign>();
}