using LoyalStamp.Data.Entities;
using LoyalStamp.Features.HistoryAndDashboard.MerchantDashboard;
using LoyalStamp.Tests.TestHelpers;

namespace LoyalStamp.Tests.Features.HistoryAndDashboard;

public class MerchantDashboardTests
{
    private const string SarahMerchantUserId = "200000000001";
    private const string JasonMerchantUserId = "200000000002";
    private const string OrphanUserId = "200000000099";
    private const string AlexCustomerUserId = "100000000001";
    private const string MeiLingCustomerUserId = "100000000002";

    private static async Task<TestDbContextFactory> SetupScenarioAsync()
    {
        var dbFactory = new TestDbContextFactory();
        await using var db = dbFactory.CreateDbContext();

        db.Users.AddRange(
            new User { Id = SarahMerchantUserId, UserName = "sarah@test.com", Email = "sarah@test.com", FullName = "Sarah Lim" },
            new User { Id = JasonMerchantUserId, UserName = "jason@test.com", Email = "jason@test.com", FullName = "Jason Ong" },
            new User { Id = OrphanUserId, UserName = "orphan@test.com", Email = "orphan@test.com", FullName = "Orphan User" },
            new User { Id = AlexCustomerUserId, UserName = "alex@test.com", Email = "alex@test.com", FullName = "Alex Tan" },
            new User { Id = MeiLingCustomerUserId, UserName = "meiling@test.com", Email = "meiling@test.com", FullName = "Mei Ling" }
        );

        db.Merchants.AddRange(
            new Merchant { Id = 1, UserId = SarahMerchantUserId, StoreName = "Artisan Brew Coffee", Description = "Specialty coffee" },
            new Merchant { Id = 2, UserId = JasonMerchantUserId, StoreName = "Boba Avenue" }
        );

        var seedCreated = new DateTime(2026, 10, 1, 8, 0, 0);
        db.StampCampaigns.AddRange(
            new StampCampaign { Id = 1, MerchantId = 1, Title = "Espresso Club", MinSpendAmount = 5m, TargetStamps = 8, RewardItem = "Free Latte", IsActive = true, CreatedAt = seedCreated },
            new StampCampaign { Id = 2, MerchantId = 1, Title = "Pastry Pass", MinSpendAmount = 10m, TargetStamps = 5, RewardItem = "Free Croissant", IsActive = false, CreatedAt = seedCreated.AddMinutes(1) },
            new StampCampaign { Id = 3, MerchantId = 2, Title = "Boba Card", MinSpendAmount = 6m, TargetStamps = 8, RewardItem = "Free Boba", IsActive = true, CreatedAt = seedCreated.AddMinutes(2) }
        );

        var alexSarahCard1 = Guid.NewGuid();
        var alexSarahCard2 = Guid.NewGuid();
        var alexJasonCard = Guid.NewGuid();
        var meiLingSarahCard = Guid.NewGuid();

        db.CustomerCards.AddRange(
            new CustomerCard { Id = alexSarahCard1, UserId = AlexCustomerUserId, CampaignId = 1, CardCode = "LS-A0001", CurrentStamps = 1, TotalRedeemedCount = 1, RowVersion = [1] },
            new CustomerCard { Id = alexSarahCard2, UserId = AlexCustomerUserId, CampaignId = 2, CardCode = "LS-A0002", CurrentStamps = 2, TotalRedeemedCount = 0, RowVersion = [1] },
            new CustomerCard { Id = alexJasonCard, UserId = AlexCustomerUserId, CampaignId = 3, CardCode = "LS-A0003", CurrentStamps = 4, TotalRedeemedCount = 0, RowVersion = [1] },
            new CustomerCard { Id = meiLingSarahCard, UserId = MeiLingCustomerUserId, CampaignId = 1, CardCode = "LS-M0001", CurrentStamps = 3, TotalRedeemedCount = 0, RowVersion = [1] }
        );

        var baseTime = new DateTime(2026, 11, 20, 9, 0, 0);

        db.StampTransactions.AddRange(
            new StampTransaction { CustomerCardId = alexSarahCard1, TransactionType = "Earn", StampsChanged = 9, SpendAmount = 48.50m, CreatedAt = baseTime },
            new StampTransaction { CustomerCardId = alexSarahCard1, TransactionType = "Redeem", StampsChanged = -8, SpendAmount = null, CreatedAt = baseTime.AddMinutes(5) },
            new StampTransaction { CustomerCardId = alexJasonCard, TransactionType = "Earn", StampsChanged = 4, SpendAmount = 25.00m, CreatedAt = baseTime.AddMinutes(10) },
            new StampTransaction { CustomerCardId = meiLingSarahCard, TransactionType = "Earn", StampsChanged = 3, SpendAmount = 16.00m, CreatedAt = baseTime.AddMinutes(15) }
        );

        await db.SaveChangesAsync();
        return dbFactory;
    }

    [Fact]
    public async Task ExecuteAsync_ValidMerchant_AggregatesStoreKpisAndDeduplicatesActiveCustomers()
    {
        using var dbFactory = await SetupScenarioAsync();
        var query = new DashboardMetricsQuery(dbFactory);

        var dto = await query.ExecuteAsync(SarahMerchantUserId);

        Assert.NotNull(dto);
        Assert.Equal("Artisan Brew Coffee", dto.StoreName);
        Assert.Equal("Specialty coffee", dto.Description);

        // Campaign counts scoped to Sarah's store only
        Assert.Equal(2, dto.TotalCampaigns);
        Assert.Equal(1, dto.ActiveCampaigns);

        // Active customers: Alex holds 2 cards at Sarah's store + Mei Ling holds 1 card = 2 distinct customers
        Assert.Equal(2, dto.TotalActiveCustomers);

        // Stamps issued at Sarah's store: Alex (+9) + Mei Ling (+3) = 12 (excludes Jason's +4 and -8 Redeem)
        Assert.Equal(12, dto.TotalStampsIssued);

        // Rewards redeemed at Sarah's store: Alex (1) + Mei Ling (0) = 1
        Assert.Equal(1, dto.TotalRewardsRedeemed);
    }

    [Fact]
    public async Task ExecuteAsync_MoreThanFiveCampaigns_ReturnsOnlyFiveMostRecentCampaigns()
    {
        using var dbFactory = await SetupScenarioAsync();
        await using (var seedDb = dbFactory.CreateDbContext())
        {
            var baseCreated = new DateTime(2026, 11, 1, 8, 0, 0);
            for (int i = 1; i <= 6; i++)
            {
                seedDb.StampCampaigns.Add(new StampCampaign
                {
                    MerchantId = 2, // Jason's store (already has 1 campaign -> total 7)
                    Title = $"Jason Promo #{i}",
                    MinSpendAmount = 5m,
                    TargetStamps = 5,
                    RewardItem = "Reward",
                    IsActive = true,
                    CreatedAt = baseCreated.AddHours(i)
                });
            }
            await seedDb.SaveChangesAsync();
        }

        var query = new DashboardMetricsQuery(dbFactory);
        var dto = await query.ExecuteAsync(JasonMerchantUserId);

        Assert.NotNull(dto);
        Assert.Equal(7, dto.TotalCampaigns);
        Assert.Equal(5, dto.RecentCampaigns.Count);
        Assert.Equal("Jason Promo #6", dto.RecentCampaigns[0].Title);
    }

    [Fact]
    public async Task ExecuteAsync_WhenMerchantProfileMissing_ReturnsNull()
    {
        using var dbFactory = await SetupScenarioAsync();
        var query = new DashboardMetricsQuery(dbFactory);

        var dto = await query.ExecuteAsync(OrphanUserId);

        Assert.Null(dto);
    }
}