using Bunit;
using LoyalStamp.Data;
using LoyalStamp.Data.Entities;
using LoyalStamp.Features.CustomerWallet.ExploreCampaigns;
using LoyalStamp.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;

namespace LoyalStamp.Tests.Components.Features.CustomerWallet;

public class ExplorePageTests : BunitContext
{
    private const string SarahMerchantId = "200000000001";
    private const string AlexCustomerId = "100000000001";

    private async Task<TestDbContextFactory> SetupPageDependenciesAsync(bool seedActiveCampaigns = true)
    {
        var dbFactory = new TestDbContextFactory();
        await using (var db = dbFactory.CreateDbContext())
        {
            db.Users.AddRange(
                new User { Id = SarahMerchantId, UserName = "sarah@test.com", Email = "sarah@test.com", FullName = "Sarah Lim" },
                new User { Id = AlexCustomerId, UserName = "alex@test.com", Email = "alex@test.com", FullName = "Alex Tan" }
            );
            db.Merchants.Add(new Merchant
            {
                Id = 1,
                UserId = SarahMerchantId,
                StoreName = "Artisan Brew Coffee",
                Description = "Specialty coffee"
            });

            if (seedActiveCampaigns)
            {
                db.StampCampaigns.AddRange(
                    new StampCampaign
                    {
                        Id = 1,
                        MerchantId = 1,
                        Title = "Daily Espresso Club",
                        MinSpendAmount = 5.00m,
                        TargetStamps = 8,
                        RewardItem = "1 Free Latte",
                        IsActive = true
                    },
                    new StampCampaign
                    {
                        Id = 2,
                        MerchantId = 1,
                        Title = "Joined Pour-Over Pass",
                        MinSpendAmount = 10.00m,
                        TargetStamps = 5,
                        RewardItem = "1 Free Pour-Over",
                        IsActive = true
                    },
                    new StampCampaign
                    {
                        Id = 3,
                        MerchantId = 1,
                        Title = "Hidden Draft Campaign",
                        MinSpendAmount = 15.00m,
                        TargetStamps = 6,
                        RewardItem = "Free Beans",
                        IsActive = false // Must be hidden from /explore
                    }
                );

                // Alex already joined Campaign 2
                db.CustomerCards.Add(new CustomerCard
                {
                    Id = Guid.NewGuid(),
                    UserId = AlexCustomerId,
                    CampaignId = 2,
                    CardCode = "LS-JOIN2",
                    CurrentStamps = 2,
                    RowVersion = [1]
                });
            }

            await db.SaveChangesAsync();
        }

        Services.AddSingleton<IDbContextFactory<ApplicationDbContext>>(dbFactory);
        Services.AddScoped<JoinCampaignCommand>();
        return dbFactory;
    }

    private void AuthorizeAsCustomer(string userId)
    {
        var authContext = this.AddAuthorization();
        authContext.SetAuthorized("alex@test.com");
        authContext.SetRoles("Customer");
        authContext.SetClaims(new Claim(ClaimTypes.NameIdentifier, userId));
    }

    [Fact]
    public async Task Render_WhenNoActiveCampaignsExist_ShowsEmptyStateCard()
    {
        // 1. Arrange
        using var dbFactory = await SetupPageDependenciesAsync(seedActiveCampaigns: false);
        AuthorizeAsCustomer(AlexCustomerId);

        // 2. Act
        var cut = Render<ExplorePage>();

        // 3. Assert
        Assert.Contains("No Active Campaigns Right Now", cut.Find(".card-body h5").TextContent);
    }

    [Fact]
    public async Task Render_WhenActiveCampaignsExist_HidesInactiveAndMarksJoinedButtonDisabled()
    {
        // 1. Arrange
        using var dbFactory = await SetupPageDependenciesAsync(seedActiveCampaigns: true);
        AuthorizeAsCustomer(AlexCustomerId);

        // 2. Act
        var cut = Render<ExplorePage>();

        // 3. Assert: Inactive campaign is not rendered
        Assert.DoesNotContain("Hidden Draft Campaign", cut.Markup);
        Assert.Contains("Daily Espresso Club", cut.Markup);
        Assert.Contains("Joined Pour-Over Pass", cut.Markup);

        // Verify 1 enabled "Join Campaign" button and 1 disabled "Already in Wallet" button
        var joinButtons = cut.FindAll("button.btn-primary");
        var joinedButtons = cut.FindAll("button.btn-outline-success[disabled]");
        Assert.Single(joinButtons);
        Assert.Single(joinedButtons);
        Assert.Contains("Already in Wallet", joinedButtons[0].TextContent);
    }

    [Fact]
    public async Task ClickJoinButton_WhenCampaignNotJoined_CreatesWalletCardAndUpdatesButtonState()
    {
        // 1. Arrange
        using var dbFactory = await SetupPageDependenciesAsync(seedActiveCampaigns: true);
        AuthorizeAsCustomer(AlexCustomerId);

        var cut = Render<ExplorePage>();

        // 2. Act: click the "Join Campaign" button for Daily Espresso Club
        await cut.Find("button.btn-primary").ClickAsync();

        // 3. Assert: success banner appears and button toggles to disabled "Already in Wallet"
        cut.WaitForAssertion(() =>
        {
            var alert = cut.Find(".alert-success");
            Assert.Contains("Stamp card added to your wallet!", alert.TextContent);
            Assert.Empty(cut.FindAll("button.btn-primary"));
            Assert.Equal(2, cut.FindAll("button.btn-outline-success[disabled]").Count);
        });

        // Verify card persisted in database with LS- prefix
        await using var verifyDb = dbFactory.CreateDbContext();
        var createdCard = await verifyDb.CustomerCards.SingleAsync(c => c.CampaignId == 1 && c.UserId == AlexCustomerId);
        Assert.StartsWith("LS-", createdCard.CardCode);
    }
}