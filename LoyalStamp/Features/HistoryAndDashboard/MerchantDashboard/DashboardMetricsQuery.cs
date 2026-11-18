using LoyalStamp.Data;
using Microsoft.EntityFrameworkCore;

namespace LoyalStamp.Features.HistoryAndDashboard.MerchantDashboard;

public record RecentCampaignItem(
    int Id,
    string Title,
    decimal MinSpendAmount,
    int TargetStamps,
    string RewardItem,
    bool IsActive,
    DateTime CreatedAt);

public class MerchantDashboardDto
{
    public string StoreName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? LogoUrl { get; set; }

    // activity metrics (current database has data)
    public int TotalCampaigns { get; set; }
    public int ActiveCampaigns { get; set; }

    // core operational KPIs (to be connected to real aggregate queries after CustomerCards & StampTransactions tables are built)
    public int TotalActiveCustomers { get; set; }
    public int TotalStampsIssued { get; set; }
    public int TotalRewardsRedeemed { get; set; }

    public List<RecentCampaignItem> RecentCampaigns { get; set; } = new();
}

public class DashboardMetricsQuery
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;

    public DashboardMetricsQuery(IDbContextFactory<ApplicationDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<MerchantDashboardDto?> ExecuteAsync(string userId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var merchant = await db.Merchants
            .AsNoTracking()
            .Include(m => m.Campaigns)
            .FirstOrDefaultAsync(m => m.UserId == userId);

        if (merchant is null)
        {
            return null;
        }

        var campaigns = merchant.Campaigns
            .OrderByDescending(c => c.CreatedAt)
            .ToList();

        var campaignIds = campaigns.Select(c => c.Id).ToList();

        int totalActiveCustomers = await db.CustomerCards
            .AsNoTracking()
            .Where(cc => campaignIds.Contains(cc.CampaignId))
            .Select(cc => cc.UserId)
            .Distinct()
            .CountAsync();

        int totalStampsIssued = await db.StampTransactions
            .AsNoTracking()
            .Where(t => campaignIds.Contains(t.CustomerCard.CampaignId) && t.TransactionType == "Earn")
            .SumAsync(t => (int?)t.StampsChanged) ?? 0;

        int totalRewardsRedeemed = await db.CustomerCards
            .AsNoTracking()
            .Where(cc => campaignIds.Contains(cc.CampaignId))
            .SumAsync(cc => (int?)cc.TotalRedeemedCount) ?? 0;

        return new MerchantDashboardDto
        {
            StoreName = merchant.StoreName,
            Description = merchant.Description,
            LogoUrl = merchant.LogoUrl,
            TotalCampaigns = campaigns.Count,
            ActiveCampaigns = campaigns.Count(c => c.IsActive),

            // default placeholder values: to be replaced with real Count/Sum queries after adding CustomerCards and StampTransactions tables
            TotalActiveCustomers = totalActiveCustomers,
            TotalStampsIssued = totalStampsIssued,
            TotalRewardsRedeemed = totalRewardsRedeemed,

            RecentCampaigns = campaigns
                .Take(5)
                .Select(c => new RecentCampaignItem(
                    c.Id,
                    c.Title,
                    c.MinSpendAmount,
                    c.TargetStamps,
                    c.RewardItem,
                    c.IsActive,
                    c.CreatedAt))
                .ToList()
        };
    }
}