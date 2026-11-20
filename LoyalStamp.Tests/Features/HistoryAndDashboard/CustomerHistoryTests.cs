using LoyalStamp.Data.Entities;
using LoyalStamp.Features.HistoryAndDashboard.CustomerHistory;
using LoyalStamp.Tests.TestHelpers;

namespace LoyalStamp.Tests.Features.HistoryAndDashboard;

public class CustomerHistoryTests
{
    private const string SarahMerchantUserId = "200000000001";
    private const string JasonMerchantUserId = "200000000002";
    private const string AlexCustomerUserId = "100000000001";
    private const string MeiLingCustomerUserId = "100000000002";
    private const string NewCustomerUserId = "100000000099";

    private static async Task<TestDbContextFactory> SetupScenarioAsync()
    {
        var dbFactory = new TestDbContextFactory();
        await using var db = dbFactory.CreateDbContext();

        db.Users.AddRange(
            new User { Id = SarahMerchantUserId, UserName = "sarah@test.com", Email = "sarah@test.com", FullName = "Sarah Lim" },
            new User { Id = JasonMerchantUserId, UserName = "jason@test.com", Email = "jason@test.com", FullName = "Jason Ong" },
            new User { Id = AlexCustomerUserId, UserName = "alex@test.com", Email = "alex@test.com", FullName = "Alex Tan" },
            new User { Id = MeiLingCustomerUserId, UserName = "meiling@test.com", Email = "meiling@test.com", FullName = "Mei Ling" },
            new User { Id = NewCustomerUserId, UserName = "new@test.com", Email = "new@test.com", FullName = "New User" }
        );

        db.Merchants.AddRange(
            new Merchant { Id = 1, UserId = SarahMerchantUserId, StoreName = "Artisan Brew Coffee" },
            new Merchant { Id = 2, UserId = JasonMerchantUserId, StoreName = "Boba Avenue" }
        );

        db.StampCampaigns.AddRange(
            new StampCampaign { Id = 1, MerchantId = 1, Title = "Espresso Club", MinSpendAmount = 5m, TargetStamps = 8, RewardItem = "Free Latte", IsActive = true },
            new StampCampaign { Id = 2, MerchantId = 2, Title = "Boba Card", MinSpendAmount = 6m, TargetStamps = 8, RewardItem = "Free Boba", IsActive = true }
        );

        var alexSarahCard = Guid.NewGuid();
        var alexJasonCard = Guid.NewGuid();
        var meiLingSarahCard = Guid.NewGuid();

        db.CustomerCards.AddRange(
            new CustomerCard { Id = alexSarahCard, UserId = AlexCustomerUserId, CampaignId = 1, CardCode = "LS-A0001", CurrentStamps = 1, TotalRedeemedCount = 1, RowVersion = [1] },
            new CustomerCard { Id = alexJasonCard, UserId = AlexCustomerUserId, CampaignId = 2, CardCode = "LS-A0002", CurrentStamps = 4, TotalRedeemedCount = 0, RowVersion = [1] },
            new CustomerCard { Id = meiLingSarahCard, UserId = MeiLingCustomerUserId, CampaignId = 1, CardCode = "LS-M0001", CurrentStamps = 3, TotalRedeemedCount = 0, RowVersion = [1] }
        );

        var baseTime = new DateTime(2026, 11, 20, 9, 0, 0);

        db.StampTransactions.AddRange(
            new StampTransaction { CustomerCardId = alexSarahCard, TransactionType = "Earn", StampsChanged = 9, SpendAmount = 48.50m, Notes = "Receipt #1", CreatedAt = baseTime },
            new StampTransaction { CustomerCardId = alexSarahCard, TransactionType = "Redeem", StampsChanged = -8, SpendAmount = null, Notes = "Redeemed Latte", CreatedAt = baseTime.AddMinutes(5) },
            new StampTransaction { CustomerCardId = alexJasonCard, TransactionType = "Earn", StampsChanged = 4, SpendAmount = 25.00m, Notes = "Receipt #2", CreatedAt = baseTime.AddMinutes(10) },
            new StampTransaction { CustomerCardId = meiLingSarahCard, TransactionType = "Earn", StampsChanged = 3, SpendAmount = 16.00m, Notes = "Mei Ling Receipt", CreatedAt = baseTime.AddMinutes(15) }
        );

        await db.SaveChangesAsync();
        return dbFactory;
    }

    [Fact]
    public async Task ExecuteAsync_WhenCalled_CalculatesMetricsOrdersDescendingAndIsolatesCustomerData()
    {
        using var dbFactory = await SetupScenarioAsync();
        var query = new HistoryQuery(dbFactory);

        var result = await query.ExecuteAsync(AlexCustomerUserId);

        // Strict customer isolation: 3 transactions for Alex, excluding Mei Ling's record
        Assert.Equal(3, result.Transactions.Count);
        Assert.DoesNotContain(result.Transactions, t => t.Notes == "Mei Ling Receipt");

        // Chronological descending order (most recent first)
        Assert.Equal("Boba Avenue", result.Transactions[0].StoreName);
        Assert.Equal("Redeem", result.Transactions[1].TransactionType);
        Assert.Equal("Earn", result.Transactions[2].TransactionType);

        // Summary KPI calculations
        Assert.Equal(13, result.TotalStampsEarned);     // 9 + 4 (ignores -8 Redeem)
        Assert.Equal(1, result.TotalRewardsRedeemed);
        Assert.Equal(73.50m, result.TotalAmountSpent);  // 48.50 + 25.00

        // Distinct alphabetically sorted store list
        Assert.Equal(["Artisan Brew Coffee", "Boba Avenue"], result.AvailableStores);
    }

    [Fact]
    public async Task ExecuteAsync_CustomerWithNoTransactions_ReturnsZeroMetricsAndEmptyLists()
    {
        using var dbFactory = await SetupScenarioAsync();
        var query = new HistoryQuery(dbFactory);

        var result = await query.ExecuteAsync(NewCustomerUserId);

        Assert.Empty(result.Transactions);
        Assert.Empty(result.AvailableStores);
        Assert.Equal(0, result.TotalStampsEarned);
        Assert.Equal(0, result.TotalRewardsRedeemed);
        Assert.Equal(0m, result.TotalAmountSpent);
    }
}