using Bunit;
using LoyalStamp.Data;
using LoyalStamp.Data.Entities;
using LoyalStamp.Features.Campaigns.ListCampaigns;
using LoyalStamp.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;

namespace LoyalStamp.Tests.Components.Features.Campaigns;

public class MerchantCampaignsPageTests : BunitContext
{
    private const string SarahUserId = "200000000001";
    private const string JasonUserId = "200000000002";

    private async Task<TestDbContextFactory> SetupPageDependenciesAsync(bool seedCampaigns = true)
    {
        var dbFactory = new TestDbContextFactory();
        await using (var db = dbFactory.CreateDbContext())
        {
            db.Users.AddRange(
                new User { Id = SarahUserId, UserName = "sarah@test.com", Email = "sarah@test.com", FullName = "Sarah Lim" },
                new User { Id = JasonUserId, UserName = "jason@test.com", Email = "jason@test.com", FullName = "Jason Ong" }
            );
            db.Merchants.AddRange(
                new Merchant { Id = 1, UserId = SarahUserId, StoreName = "Artisan Brew Coffee" },
                new Merchant { Id = 2, UserId = JasonUserId, StoreName = "Boba Avenue" }
            );

            if (seedCampaigns)
            {
                var baseTime = new DateTime(2026, 11, 1, 10, 0, 0);
                db.StampCampaigns.AddRange(
                    new StampCampaign
                    {
                        Id = 1,
                        MerchantId = 1,
                        Title = "Daily Espresso Club",
                        MinSpendAmount = 5.00m,
                        TargetStamps = 8,
                        RewardItem = "1 Free Latte",
                        IsActive = true,
                        CreatedAt = baseTime.AddHours(2) // Most recent -> renders first
                    },
                    new StampCampaign
                    {
                        Id = 2,
                        MerchantId = 1,
                        Title = "Weekend Roaster Reserve",
                        MinSpendAmount = 25.00m,
                        TargetStamps = 5,
                        RewardItem = "Free Bean Bag",
                        IsActive = false,
                        CreatedAt = baseTime.AddHours(1) // Older -> renders second
                    },
                    new StampCampaign
                    {
                        Id = 3,
                        MerchantId = 2, // Belongs to Jason -> must not render for Sarah
                        Title = "Jason Boba Pass",
                        MinSpendAmount = 6.00m,
                        TargetStamps = 8,
                        RewardItem = "Free Boba",
                        IsActive = true,
                        CreatedAt = baseTime.AddHours(3)
                    }
                );
            }

            await db.SaveChangesAsync();
        }

        Services.AddSingleton<IDbContextFactory<ApplicationDbContext>>(dbFactory);
        Services.AddScoped<ListCampaignsQuery>();
        return dbFactory;
    }

    private void AuthorizeAsMerchant(string userId, string email)
    {
        var authContext = this.AddAuthorization();
        authContext.SetAuthorized(email);
        authContext.SetRoles("Merchant");
        authContext.SetClaims(new Claim(ClaimTypes.NameIdentifier, userId));
    }

    [Fact]
    public async Task Render_WhenMerchantHasNoCampaigns_ShowsEmptyStatePrompt()
    {
        // 1. Arrange
        using var dbFactory = await SetupPageDependenciesAsync(seedCampaigns: false);
        AuthorizeAsMerchant(SarahUserId, "sarah@test.com");

        // 2. Act
        var cut = Render<MerchantCampaignsPage>();

        // 3. Assert
        Assert.Contains("Artisan Brew Coffee", cut.Find("p.text-muted").TextContent);
        Assert.Contains("No Stamp Campaigns Yet", cut.Find(".card-body h5").TextContent);
        Assert.NotEmpty(cut.FindAll("a[href='/merchant/campaigns/create']"));
    }

    [Fact]
    public async Task Render_WhenMerchantHasCampaigns_DisplaysOnlyOwnedCampaignCardsWithStatusesAndEditLinks()
    {
        // 1. Arrange
        using var dbFactory = await SetupPageDependenciesAsync(seedCampaigns: true);
        AuthorizeAsMerchant(SarahUserId, "sarah@test.com");

        // 2. Act
        var cut = Render<MerchantCampaignsPage>();

        // 3. Assert: 2 cards for Sarah, Jason's campaign is excluded
        var titles = cut.FindAll(".card-title").Select(t => t.TextContent.Trim()).ToList();
        Assert.Equal(["Daily Espresso Club", "Weekend Roaster Reserve"], titles);
        Assert.DoesNotContain("Jason Boba Pass", titles);

        // Verify Active and Inactive status badges
        var badges = cut.FindAll(".card-header .badge").Select(b => b.TextContent.Trim()).ToList();
        Assert.Equal(["Active", "Inactive"], badges);

        // Verify Edit links point to the right campaign IDs
        Assert.NotEmpty(cut.FindAll("a[href='/merchant/campaigns/edit/1']"));
        Assert.NotEmpty(cut.FindAll("a[href='/merchant/campaigns/edit/2']"));
    }

    [Fact]
    public async Task ToggleSwitch_WhenClickedOnInactiveCampaign_UpdatesStatusBadgeAndPersistsToDatabase()
    {
        // 1. Arrange
        using var dbFactory = await SetupPageDependenciesAsync(seedCampaigns: true);
        AuthorizeAsMerchant(SarahUserId, "sarah@test.com");

        var cut = Render<MerchantCampaignsPage>();

        // 2. Act: second switch belongs to "Weekend Roaster Reserve" (Id = 2, initially Inactive)
        cut.FindAll("input[type='checkbox'][role='switch']")[1].Change(true);

        // 3. Assert: verify UI badge updated to Active and database persisted IsActive = true
        cut.WaitForAssertion(() =>
        {
            var badges = cut.FindAll(".card-header .badge").Select(b => b.TextContent.Trim()).ToList();
            Assert.Equal(["Active", "Active"], badges);
        });

        await using var verifyDb = dbFactory.CreateDbContext();
        var updated = await verifyDb.StampCampaigns.FindAsync(2);
        Assert.True(updated!.IsActive);
    }
}