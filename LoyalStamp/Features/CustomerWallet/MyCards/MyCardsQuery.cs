using LoyalStamp.Data;
using Microsoft.EntityFrameworkCore;

namespace LoyalStamp.Features.CustomerWallet.MyCards;

public record CustomerCardDto(
    Guid CardId,
    string CardCode,
    string StoreName,
    string? LogoUrl,
    string CampaignTitle,
    decimal MinSpendAmount,
    int CurrentStamps,
    int TargetStamps,
    string RewardItem,
    int TotalRedeemedCount,
    bool IsCampaignActive,
    DateTime LastUpdated);

public class MyCardsQuery
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;

    public MyCardsQuery(IDbContextFactory<ApplicationDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<List<CustomerCardDto>> ExecuteAsync(string userId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        return await db.CustomerCards
            .AsNoTracking()
            .Include(cc => cc.Campaign)
                .ThenInclude(c => c.Merchant)
            .Where(cc => cc.UserId == userId)
            .OrderByDescending(cc => cc.LastUpdated)
            .Select(cc => new CustomerCardDto(
                cc.Id,
                cc.CardCode,
                cc.Campaign.Merchant.StoreName,
                cc.Campaign.Merchant.LogoUrl,
                cc.Campaign.Title,
                cc.Campaign.MinSpendAmount,
                cc.CurrentStamps,
                cc.Campaign.TargetStamps,
                cc.Campaign.RewardItem,
                cc.TotalRedeemedCount,
                cc.Campaign.IsActive,
                cc.LastUpdated))
            .ToListAsync();
    }
}