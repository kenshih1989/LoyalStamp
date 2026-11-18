using LoyalStamp.Data;
using Microsoft.EntityFrameworkCore;

namespace LoyalStamp.Features.HistoryAndDashboard.CustomerHistory;

public record TransactionHistoryItemDto(
    int TransactionId,
    DateTime CreatedAt,
    string StoreName,
    string? StoreLogoUrl,
    string CampaignTitle,
    string CardCode,
    string TransactionType, // "Earn" or "Redeem"
    int StampsChanged,
    decimal? SpendAmount,
    string? Notes);

public class CustomerHistoryResult
{
    public int TotalStampsEarned { get; set; }
    public int TotalRewardsRedeemed { get; set; }
    public decimal TotalAmountSpent { get; set; }
    public List<string> AvailableStores { get; set; } = new();
    public List<TransactionHistoryItemDto> Transactions { get; set; } = new();
}

public class HistoryQuery
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;

    public HistoryQuery(IDbContextFactory<ApplicationDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<CustomerHistoryResult> ExecuteAsync(string userId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        // Enforce strict ownership filter: only fetch transactions belonging to the logged-in customer
        var transactions = await db.StampTransactions
            .AsNoTracking()
            .Include(t => t.CustomerCard)
                .ThenInclude(cc => cc.Campaign)
                    .ThenInclude(c => c.Merchant)
            .Where(t => t.CustomerCard.UserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new TransactionHistoryItemDto(
                t.Id,
                t.CreatedAt,
                t.CustomerCard.Campaign.Merchant.StoreName,
                t.CustomerCard.Campaign.Merchant.LogoUrl,
                t.CustomerCard.Campaign.Title,
                t.CustomerCard.CardCode,
                t.TransactionType,
                t.StampsChanged,
                t.SpendAmount,
                t.Notes))
            .ToListAsync();

        var result = new CustomerHistoryResult
        {
            Transactions = transactions,
            TotalStampsEarned = transactions
                .Where(t => t.TransactionType == "Earn")
                .Sum(t => t.StampsChanged),
            TotalRewardsRedeemed = transactions
                .Count(t => t.TransactionType == "Redeem"),
            TotalAmountSpent = transactions
                .Where(t => t.TransactionType == "Earn" && t.SpendAmount.HasValue)
                .Sum(t => t.SpendAmount!.Value),
            AvailableStores = transactions
                .Select(t => t.StoreName)
                .Distinct()
                .OrderBy(name => name)
                .ToList()
        };

        return result;
    }
}