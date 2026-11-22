using LoyalStamp.Data.Entities;
using LoyalStamp.Features.CustomerWallet.ExploreCampaigns;
using LoyalStamp.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;

namespace LoyalStamp.Tests.Unit.Features.CustomerWallet;

public class ExploreCampaignsTests
{
    private const string SarahMerchantUserId = "200000000001";
    private const string JasonMerchantUserId = "200000000002";
    private const string AlexCustomerUserId = "100000000001";

    private static async Task<TestDbContextFactory> SetupScenarioAsync()
    {
        var dbFactory = new TestDbContextFactory();
        await using var db = dbFactory.CreateDbContext();

        db.Users.AddRange(
            new User { Id = SarahMerchantUserId, UserName = "sarah@test.com", Email = "sarah@test.com", FullName = "Sarah Lim" },
            new User { Id = JasonMerchantUserId, UserName = "jason@test.com", Email = "jason@test.com", FullName = "Jason Ong" },
            new User { Id = AlexCustomerUserId, UserName = "alex@test.com", Email = "alex@test.com", FullName = "Alex Tan" }
        );
        db.Merchants.AddRange(
            new Merchant { Id = 1, UserId = SarahMerchantUserId, StoreName = "Artisan Brew Coffee" },
            new Merchant { Id = 2, UserId = JasonMerchantUserId, StoreName = "Boba Avenue" }
        );
        db.StampCampaigns.AddRange(
            new StampCampaign { Id = 1, MerchantId = 1, Title = "Daily Espresso Club", MinSpendAmount = 5.00m, TargetStamps = 8, RewardItem = "Free Latte", IsActive = true },
            new StampCampaign { Id = 2, MerchantId = 2, Title = "Brown Sugar Boba Pass", MinSpendAmount = 6.00m, TargetStamps = 8, RewardItem = "Free Boba", IsActive = true },
            new StampCampaign { Id = 3, MerchantId = 1, Title = "Secret Draft Campaign", MinSpendAmount = 25.00m, TargetStamps = 5, RewardItem = "Beans", IsActive = false }
        );

        await db.SaveChangesAsync();
        return dbFactory;
    }

    [Fact]
    public async Task GetActiveCampaignsAsync_WhenCalled_ExcludesInactiveAndMarksJoinedCards()
    {
        using var dbFactory = await SetupScenarioAsync();
        await using (var seedDb = dbFactory.CreateDbContext())
        {
            seedDb.CustomerCards.Add(new CustomerCard
            {
                Id = Guid.NewGuid(),
                UserId = AlexCustomerUserId,
                CampaignId = 1,
                CardCode = "LS-EXIST",
                RowVersion = [1]
            });
            await seedDb.SaveChangesAsync();
        }

        var command = new JoinCampaignCommand(dbFactory);
        var campaigns = await command.GetActiveCampaignsAsync(AlexCustomerUserId);

        Assert.Equal(2, campaigns.Count);
        Assert.DoesNotContain(campaigns, c => c.CampaignId == 3);
        Assert.True(campaigns.Single(c => c.CampaignId == 1).IsAlreadyJoined);
        Assert.False(campaigns.Single(c => c.CampaignId == 2).IsAlreadyJoined);
    }

    [Fact]
    public async Task JoinAsync_ValidActiveCampaign_CreatesCardWithFormattedShortCode()
    {
        using var dbFactory = await SetupScenarioAsync();
        var command = new JoinCampaignCommand(dbFactory);

        var (succeeded, error) = await command.JoinAsync(1, AlexCustomerUserId);

        Assert.True(succeeded);
        Assert.Null(error);

        await using var verifyDb = dbFactory.CreateDbContext();
        var createdCard = await verifyDb.CustomerCards.SingleAsync();
        Assert.Equal(AlexCustomerUserId, createdCard.UserId);
        Assert.Equal(1, createdCard.CampaignId);
        Assert.Equal(0, createdCard.CurrentStamps);
        Assert.StartsWith("LS-", createdCard.CardCode);
        Assert.Equal(8, createdCard.CardCode.Length);
    }

    [Fact]
    public async Task JoinAsync_DuplicateEnrollment_BlocksSecondCardCreation()
    {
        using var dbFactory = await SetupScenarioAsync();
        var command = new JoinCampaignCommand(dbFactory);

        var firstAttempt = await command.JoinAsync(1, AlexCustomerUserId);
        var secondAttempt = await command.JoinAsync(1, AlexCustomerUserId);

        Assert.True(firstAttempt.Succeeded);
        Assert.False(secondAttempt.Succeeded);
        Assert.Contains("already have this stamp card", secondAttempt.ErrorMessage);

        await using var verifyDb = dbFactory.CreateDbContext();
        Assert.Equal(1, await verifyDb.CustomerCards.CountAsync());
    }

    [Theory]
    [InlineData(3)]   // Inactive campaign
    [InlineData(999)] // Non-existent campaign
    public async Task JoinAsync_InactiveOrMissingCampaign_ReturnsFailure(int invalidCampaignId)
    {
        using var dbFactory = await SetupScenarioAsync();
        var command = new JoinCampaignCommand(dbFactory);

        var (succeeded, error) = await command.JoinAsync(invalidCampaignId, AlexCustomerUserId);

        Assert.False(succeeded);
        Assert.Contains("no longer available", error);
    }
}