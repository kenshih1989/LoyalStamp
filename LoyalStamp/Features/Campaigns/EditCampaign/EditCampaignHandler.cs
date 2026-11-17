using LoyalStamp.Data;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace LoyalStamp.Features.Campaigns.EditCampaign;

public class EditCampaignRequest
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Campaign title is required.")]
    [MaxLength(100, ErrorMessage = "Title cannot exceed 100 characters.")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Minimum spend amount is required.")]
    [Range(0.01, 100000.00, ErrorMessage = "Minimum spend must be greater than $0.00.")]
    public decimal MinSpendAmount { get; set; }

    [Required(ErrorMessage = "Target stamps count is required.")]
    [Range(1, 100, ErrorMessage = "Target stamps must be between 1 and 100.")]
    public int TargetStamps { get; set; }

    [Required(ErrorMessage = "Reward item description is required.")]
    [MaxLength(150, ErrorMessage = "Reward description cannot exceed 150 characters.")]
    public string RewardItem { get; set; } = string.Empty;

    public bool IsActive { get; set; }
}

public class EditCampaignHandler
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;

    public EditCampaignHandler(IDbContextFactory<ApplicationDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<EditCampaignRequest?> GetByIdAsync(int campaignId, string userId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var campaign = await db.StampCampaigns
            .AsNoTracking()
            .Include(c => c.Merchant)
            .FirstOrDefaultAsync(c => c.Id == campaignId && c.Merchant.UserId == userId);

        if (campaign is null)
        {
            return null;
        }

        return new EditCampaignRequest
        {
            Id = campaign.Id,
            Title = campaign.Title,
            MinSpendAmount = campaign.MinSpendAmount,
            TargetStamps = campaign.TargetStamps,
            RewardItem = campaign.RewardItem,
            IsActive = campaign.IsActive
        };
    }

    public async Task<(bool Succeeded, string? ErrorMessage)> HandleAsync(EditCampaignRequest request, string userId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var campaign = await db.StampCampaigns
            .Include(c => c.Merchant)
            .FirstOrDefaultAsync(c => c.Id == request.Id && c.Merchant.UserId == userId);

        if (campaign is null)
        {
            return (false, "Campaign not found or you do not have permission to edit it.");
        }

        campaign.Title = request.Title.Trim();
        campaign.MinSpendAmount = Math.Round(request.MinSpendAmount, 2);
        campaign.TargetStamps = request.TargetStamps;
        campaign.RewardItem = request.RewardItem.Trim();
        campaign.IsActive = request.IsActive;

        await db.SaveChangesAsync();
        return (true, null);
    }
}