using LoyalStamp.Data;
using LoyalStamp.Data.Entities;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace LoyalStamp.Features.Campaigns.CreateCampaign;

public class CreateCampaignRequest
{
    [Required(ErrorMessage = "Campaign title is required.")]
    [MaxLength(100, ErrorMessage = "Title cannot exceed 100 characters.")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Minimum spend amount is required.")]
    [Range(0.01, 100000.00, ErrorMessage = "Minimum spend must be greater than $0.00.")]
    public decimal MinSpendAmount { get; set; } = 10.00m;

    [Required(ErrorMessage = "Target stamps count is required.")]
    [Range(1, 100, ErrorMessage = "Target stamps must be between 1 and 100.")]
    public int TargetStamps { get; set; } = 10;

    [Required(ErrorMessage = "Reward item description is required.")]
    [MaxLength(150, ErrorMessage = "Reward description cannot exceed 150 characters.")]
    public string RewardItem { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}

public class CreateCampaignHandler
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;

    public CreateCampaignHandler(IDbContextFactory<ApplicationDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<(bool Succeeded, string? ErrorMessage)> HandleAsync(CreateCampaignRequest request, string userId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var merchant = await db.Merchants.FirstOrDefaultAsync(m => m.UserId == userId);
        if (merchant is null)
        {
            return (false, "Store profile not found. Please complete your merchant store setup first.");
        }

        var campaign = new StampCampaign
        {
            MerchantId = merchant.Id,
            Title = request.Title.Trim(),
            MinSpendAmount = Math.Round(request.MinSpendAmount, 2),
            TargetStamps = request.TargetStamps,
            RewardItem = request.RewardItem.Trim(),
            IsActive = request.IsActive,
            CreatedAt = DateTime.Now
        };

        db.StampCampaigns.Add(campaign);
        await db.SaveChangesAsync();

        return (true, null);
    }
}