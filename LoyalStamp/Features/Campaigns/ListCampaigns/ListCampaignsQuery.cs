using LoyalStamp.Data;
using Microsoft.EntityFrameworkCore;

namespace LoyalStamp.Features.Campaigns.ListCampaigns;

public record CampaignSummaryDto(
    int Id,
    string Title,
    decimal MinSpendAmount,
    int TargetStamps,
    string RewardItem,
    bool IsActive,
    DateTime CreatedAt);

public class ListCampaignsQuery
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;

    public ListCampaignsQuery(IDbContextFactory<ApplicationDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<(string StoreName, List<CampaignSummaryDto> Campaigns)> ExecuteAsync(string userId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var merchant = await db.Merchants
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.UserId == userId);

        if (merchant is null)
        {
            return (string.Empty, new List<CampaignSummaryDto>());
        }

        var campaigns = await db.StampCampaigns
            .AsNoTracking()
            .Where(c => c.MerchantId == merchant.Id)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new CampaignSummaryDto(
                c.Id,
                c.Title,
                c.MinSpendAmount,
                c.TargetStamps,
                c.RewardItem,
                c.IsActive,
                c.CreatedAt))
            .ToListAsync();

        return (merchant.StoreName, campaigns);
    }

    public async Task<bool> ToggleStatusAsync(int campaignId, string userId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        // Security check: Ensure that the campaign belongs to the currently logged-in merchant's store
        var campaign = await db.StampCampaigns
            .Include(c => c.Merchant)
            .FirstOrDefaultAsync(c => c.Id == campaignId && c.Merchant.UserId == userId);

        if (campaign is null)
        {
            return false;
        }

        campaign.IsActive = !campaign.IsActive;
        await db.SaveChangesAsync();
        return true;
    }
}