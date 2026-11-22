using LoyalStamp.Data.Entities;
using LoyalStamp.Features.Campaigns.CreateCampaign;
using LoyalStamp.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;

namespace LoyalStamp.Tests.Unit.Features.Campaigns;

public class CreateCampaignTests
{
    private const string SarahUserId = "200000000001";
    private const string OrphanUserId = "200000000099";

    private static async Task<TestDbContextFactory> SetupScenarioAsync()
    {
        var dbFactory = new TestDbContextFactory();
        await using var db = dbFactory.CreateDbContext();

        db.Users.AddRange(
            new User { Id = SarahUserId, UserName = "sarah@test.com", Email = "sarah@test.com", FullName = "Sarah Lim" },
            new User { Id = OrphanUserId, UserName = "orphan@test.com", Email = "orphan@test.com", FullName = "No Store" }
        );
        db.Merchants.Add(new Merchant { Id = 1, UserId = SarahUserId, StoreName = "Artisan Brew Coffee" });

        await db.SaveChangesAsync();
        return dbFactory;
    }

    [Fact]
    public async Task HandleAsync_WithValidMerchant_SavesTrimmedCampaignToStore()
    {
        using var dbFactory = await SetupScenarioAsync();
        var handler = new CreateCampaignHandler(dbFactory);

        var request = new CreateCampaignRequest
        {
            Title = "  Daily Espresso Club  ",
            MinSpendAmount = 5.556m,
            TargetStamps = 8,
            RewardItem = "  1 Free Handcrafted Latte  ",
            IsActive = true
        };

        var (succeeded, error) = await handler.HandleAsync(request, SarahUserId);

        Assert.True(succeeded);
        Assert.Null(error);

        await using var verifyDb = dbFactory.CreateDbContext();
        var saved = await verifyDb.StampCampaigns.SingleAsync();
        Assert.Equal(1, saved.MerchantId);
        Assert.Equal("Daily Espresso Club", saved.Title);
        Assert.Equal(5.56m, saved.MinSpendAmount);
        Assert.Equal(8, saved.TargetStamps);
        Assert.Equal("1 Free Handcrafted Latte", saved.RewardItem);
        Assert.True(saved.IsActive);
    }

    [Fact]
    public async Task HandleAsync_WhenMerchantProfileMissing_ReturnsFailure()
    {
        using var dbFactory = await SetupScenarioAsync();
        var handler = new CreateCampaignHandler(dbFactory);

        var request = new CreateCampaignRequest
        {
            Title = "Ghost Campaign",
            MinSpendAmount = 10.00m,
            TargetStamps = 10,
            RewardItem = "Free Item"
        };

        var (succeeded, error) = await handler.HandleAsync(request, OrphanUserId);

        Assert.False(succeeded);
        Assert.Contains("Store profile not found", error);
    }
}