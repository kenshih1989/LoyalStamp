using LoyalStamp.Data.Entities;
using LoyalStamp.Features.Campaigns.ListCampaigns;
using LoyalStamp.Tests.TestHelpers;

namespace LoyalStamp.Tests.Unit.Features.Campaigns;

public class ListCampaignsTests
{
    private const string SarahUserId = "200000000001";
    private const string JasonUserId = "200000000002";

    private static async Task<TestDbContextFactory> SetupScenarioAsync()
    {
        var dbFactory = new TestDbContextFactory();
        await using var db = dbFactory.CreateDbContext();

        db.Users.AddRange(
            new User { Id = SarahUserId, UserName = "sarah@test.com", Email = "sarah@test.com", FullName = "Sarah Lim" },
            new User { Id = JasonUserId, UserName = "jason@test.com", Email = "jason@test.com", FullName = "Jason Ong" }
        );
        db.Merchants.AddRange(
            new Merchant { Id = 1, UserId = SarahUserId, StoreName = "Artisan Brew Coffee" },
            new Merchant { Id = 2, UserId = JasonUserId, StoreName = "Boba Avenue" }
        );
        db.StampCampaigns.AddRange(
            new StampCampaign { Id = 1, MerchantId = 1, Title = "Sarah Coffee Card", MinSpendAmount = 5m, TargetStamps = 8, RewardItem = "Latte", IsActive = true },
            new StampCampaign { Id = 2, MerchantId = 1, Title = "Sarah Roaster VIP", MinSpendAmount = 20m, TargetStamps = 5, RewardItem = "Beans", IsActive = false },
            new StampCampaign { Id = 3, MerchantId = 2, Title = "Jason Boba Pass", MinSpendAmount = 6m, TargetStamps = 8, RewardItem = "Boba", IsActive = true }
        );

        await db.SaveChangesAsync();
        return dbFactory;
    }

    [Fact]
    public async Task ExecuteAsync_WhenCalled_ReturnsOnlyCurrentMerchantCampaigns()
    {
        using var dbFactory = await SetupScenarioAsync();
        var query = new ListCampaignsQuery(dbFactory);

        var (storeName, campaigns) = await query.ExecuteAsync(SarahUserId);

        Assert.Equal("Artisan Brew Coffee", storeName);
        Assert.Equal(2, campaigns.Count);
        Assert.DoesNotContain(campaigns, c => c.Title == "Jason Boba Pass");
    }

    [Fact]
    public async Task ToggleStatusAsync_OwnedCampaign_FlipsIsActiveFlag()
    {
        using var dbFactory = await SetupScenarioAsync();
        var query = new ListCampaignsQuery(dbFactory);

        var toggled = await query.ToggleStatusAsync(2, SarahUserId); // Campaign 2 starts as false

        Assert.True(toggled);

        await using var verifyDb = dbFactory.CreateDbContext();
        var updated = await verifyDb.StampCampaigns.FindAsync(2);
        Assert.True(updated!.IsActive);
    }

    [Fact]
    public async Task ToggleStatusAsync_AnotherMerchantCampaign_BlocksCrossStoreModification()
    {
        using var dbFactory = await SetupScenarioAsync();
        var query = new ListCampaignsQuery(dbFactory);

        // Jason tries to toggle Sarah's active Campaign 1
        var toggled = await query.ToggleStatusAsync(1, JasonUserId);

        Assert.False(toggled);

        await using var verifyDb = dbFactory.CreateDbContext();
        var untouched = await verifyDb.StampCampaigns.FindAsync(1);
        Assert.True(untouched!.IsActive);
    }
}