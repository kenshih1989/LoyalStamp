using LoyalStamp.Data.Entities;
using LoyalStamp.Features.CustomerWallet.MyCards;
using LoyalStamp.Tests.TestHelpers;

namespace LoyalStamp.Tests.Features.CustomerWallet;

public class MyCardsTests
{
    private const string SarahMerchantUserId = "200000000001";
    private const string JasonMerchantUserId = "200000000002";
    private const string AlexCustomerUserId = "100000000001";
    private const string MeiLingCustomerUserId = "100000000002";

    private static async Task<TestDbContextFactory> SetupScenarioAsync()
    {
        var dbFactory = new TestDbContextFactory();
        await using var db = dbFactory.CreateDbContext();

        db.Users.AddRange(
            new User { Id = SarahMerchantUserId, UserName = "sarah@test.com", Email = "sarah@test.com", FullName = "Sarah Lim" },
            new User { Id = JasonMerchantUserId, UserName = "jason@test.com", Email = "jason@test.com", FullName = "Jason Ong" },
            new User { Id = AlexCustomerUserId, UserName = "alex@test.com", Email = "alex@test.com", FullName = "Alex Tan" },
            new User { Id = MeiLingCustomerUserId, UserName = "meiling@test.com", Email = "meiling@test.com", FullName = "Mei Ling" }
        );
        db.Merchants.AddRange(
            new Merchant { Id = 1, UserId = SarahMerchantUserId, StoreName = "Artisan Brew Coffee" },
            new Merchant { Id = 2, UserId = JasonMerchantUserId, StoreName = "Boba Avenue" }
        );
        db.StampCampaigns.AddRange(
            new StampCampaign { Id = 1, MerchantId = 1, Title = "Daily Espresso Club", MinSpendAmount = 5.00m, TargetStamps = 8, RewardItem = "Free Latte", IsActive = true },
            new StampCampaign { Id = 2, MerchantId = 2, Title = "Brown Sugar Boba Pass", MinSpendAmount = 6.00m, TargetStamps = 8, RewardItem = "Free Boba", IsActive = true }
        );

        await db.SaveChangesAsync();
        return dbFactory;
    }

    [Fact]
    public async Task ExecuteAsync_WhenCalled_ReturnsOnlyCurrentCustomerCardsOrderedByLastUpdated()
    {
        using var dbFactory = await SetupScenarioAsync();
        var olderTime = new DateTime(2026, 11, 1, 10, 0, 0);
        var newerTime = new DateTime(2026, 11, 20, 14, 30, 0);

        await using (var seedDb = dbFactory.CreateDbContext())
        {
            seedDb.CustomerCards.AddRange(
                new CustomerCard
                {
                    Id = Guid.NewGuid(),
                    UserId = AlexCustomerUserId,
                    CampaignId = 1,
                    CardCode = "LS-OLDER",
                    CurrentStamps = 3,
                    LastUpdated = olderTime,
                    RowVersion = [1]
                },
                new CustomerCard
                {
                    Id = Guid.NewGuid(),
                    UserId = AlexCustomerUserId,
                    CampaignId = 2,
                    CardCode = "LS-NEWER",
                    CurrentStamps = 6,
                    LastUpdated = newerTime,
                    RowVersion = [1]
                },
                new CustomerCard
                {
                    Id = Guid.NewGuid(),
                    UserId = MeiLingCustomerUserId,
                    CampaignId = 1,
                    CardCode = "LS-OTHER",
                    CurrentStamps = 8,
                    LastUpdated = newerTime,
                    RowVersion = [1]
                }
            );
            await seedDb.SaveChangesAsync();
        }

        var query = new MyCardsQuery(dbFactory);
        var alexCards = await query.ExecuteAsync(AlexCustomerUserId);

        Assert.Equal(2, alexCards.Count);
        Assert.DoesNotContain(alexCards, c => c.CardCode == "LS-OTHER");

        Assert.Equal("LS-NEWER", alexCards[0].CardCode);
        Assert.Equal("Boba Avenue", alexCards[0].StoreName);
        Assert.Equal(6, alexCards[0].CurrentStamps);

        Assert.Equal("LS-OLDER", alexCards[1].CardCode);
        Assert.Equal("Artisan Brew Coffee", alexCards[1].StoreName);
        Assert.Equal(3, alexCards[1].CurrentStamps);
    }

    [Fact]
    public async Task ExecuteAsync_CustomerWithNoCards_ReturnsEmptyList()
    {
        using var dbFactory = await SetupScenarioAsync();
        var query = new MyCardsQuery(dbFactory);

        var cards = await query.ExecuteAsync(MeiLingCustomerUserId);

        Assert.Empty(cards);
    }
}