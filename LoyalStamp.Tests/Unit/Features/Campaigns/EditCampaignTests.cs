using LoyalStamp.Data.Entities;
using LoyalStamp.Features.Campaigns.EditCampaign;
using LoyalStamp.Tests.TestHelpers;

namespace LoyalStamp.Tests.Unit.Features.Campaigns;

public class EditCampaignTests
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
        db.StampCampaigns.Add(new StampCampaign
        {
            Id = 20,
            MerchantId = 1, // Belongs to Sarah
            Title = "Original Title",
            MinSpendAmount = 5.00m,
            TargetStamps = 8,
            RewardItem = "Original Reward",
            IsActive = true
        });

        await db.SaveChangesAsync();
        return dbFactory;
    }

    [Fact]
    public async Task GetByIdAsync_OwnedCampaign_ReturnsCampaignDetails()
    {
        using var dbFactory = await SetupScenarioAsync();
        var handler = new EditCampaignHandler(dbFactory);

        var result = await handler.GetByIdAsync(20, SarahUserId);

        Assert.NotNull(result);
        Assert.Equal("Original Title", result.Title);
    }

    [Fact]
    public async Task GetByIdAsync_AnotherMerchantCampaign_BlocksCrossMerchantRead()
    {
        using var dbFactory = await SetupScenarioAsync();
        var handler = new EditCampaignHandler(dbFactory);

        var result = await handler.GetByIdAsync(20, JasonUserId);

        Assert.Null(result);
    }

    [Fact]
    public async Task HandleAsync_OwnedCampaign_UpdatesCampaignFields()
    {
        using var dbFactory = await SetupScenarioAsync();
        var handler = new EditCampaignHandler(dbFactory);

        var update = new EditCampaignRequest
        {
            Id = 20,
            Title = "  Updated Espresso Club  ",
            MinSpendAmount = 6.50m,
            TargetStamps = 10,
            RewardItem = "  Free Pour-Over  ",
            IsActive = false
        };

        var (succeeded, error) = await handler.HandleAsync(update, SarahUserId);

        Assert.True(succeeded);
        Assert.Null(error);

        await using var verifyDb = dbFactory.CreateDbContext();
        var updated = await verifyDb.StampCampaigns.FindAsync(20);
        Assert.Equal("Updated Espresso Club", updated!.Title);
        Assert.Equal(6.50m, updated.MinSpendAmount);
        Assert.Equal(10, updated.TargetStamps);
        Assert.Equal("Free Pour-Over", updated.RewardItem);
        Assert.False(updated.IsActive);
    }

    [Fact]
    public async Task HandleAsync_AnotherMerchantCampaign_BlocksCrossMerchantModification()
    {
        using var dbFactory = await SetupScenarioAsync();
        var handler = new EditCampaignHandler(dbFactory);

        var maliciousUpdate = new EditCampaignRequest
        {
            Id = 20,
            Title = "Hacked Title",
            MinSpendAmount = 1.00m,
            TargetStamps = 1,
            RewardItem = "Free Store",
            IsActive = true
        };

        var (succeeded, error) = await handler.HandleAsync(maliciousUpdate, JasonUserId);

        Assert.False(succeeded);
        Assert.Contains("do not have permission", error);
    }
}