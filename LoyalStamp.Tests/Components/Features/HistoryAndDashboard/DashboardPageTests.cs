using Bunit;
using LoyalStamp.Data;
using LoyalStamp.Data.Entities;
using LoyalStamp.Features.HistoryAndDashboard.MerchantDashboard;
using LoyalStamp.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;

namespace LoyalStamp.Tests.Components.Features.HistoryAndDashboard;

public class DashboardPageTests : BunitContext
{
    private const string SarahMerchantId = "200000000001";
    private const string OrphanMerchantId = "200000000099";
    private const string AlexCustomerId = "100000000001";
    private const string MeiLingCustomerId = "100000000002";

    private async Task<TestDbContextFactory> SetupPageDependenciesAsync()
    {
        var dbFactory = new TestDbContextFactory();
        await using (var db = dbFactory.CreateDbContext())
        {
            db.Users.AddRange(
                new User { Id = SarahMerchantId, UserName = "sarah@test.com", Email = "sarah@test.com", FullName = "Sarah Lim" },
                new User { Id = OrphanMerchantId, UserName = "orphan@test.com", Email = "orphan@test.com", FullName = "Orphan User" },
                new User { Id = AlexCustomerId, UserName = "alex@test.com", Email = "alex@test.com", FullName = "Alex Tan" },
                new User { Id = MeiLingCustomerId, UserName = "meiling@test.com", Email = "meiling@test.com", FullName = "Mei Ling" }
            );

            db.Merchants.Add(new Merchant
            {
                Id = 1,
                UserId = SarahMerchantId,
                StoreName = "Artisan Brew Coffee",
                Description = "Specialty single-origin pour-over coffee"
            });

            var baseTime = new DateTime(2026, 11, 10, 9, 0, 0);
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
                    CreatedAt = baseTime.AddHours(2)
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
                    CreatedAt = baseTime.AddHours(1)
                }
            );

            var card1 = Guid.NewGuid();
            var card2 = Guid.NewGuid();
            db.CustomerCards.AddRange(
                new CustomerCard { Id = card1, UserId = AlexCustomerId, CampaignId = 1, CardCode = "LS-A0001", CurrentStamps = 1, TotalRedeemedCount = 1, RowVersion = [1] },
                new CustomerCard { Id = card2, UserId = MeiLingCustomerId, CampaignId = 1, CardCode = "LS-M0001", CurrentStamps = 4, TotalRedeemedCount = 0, RowVersion = [1] }
            );

            db.StampTransactions.AddRange(
                new StampTransaction { CustomerCardId = card1, TransactionType = "Earn", StampsChanged = 9, SpendAmount = 48.50m, CreatedAt = baseTime },
                new StampTransaction { CustomerCardId = card1, TransactionType = "Redeem", StampsChanged = -8, SpendAmount = null, CreatedAt = baseTime.AddMinutes(5) },
                new StampTransaction { CustomerCardId = card2, TransactionType = "Earn", StampsChanged = 4, SpendAmount = 22.00m, CreatedAt = baseTime.AddMinutes(10) }
            );

            await db.SaveChangesAsync();
        }

        Services.AddSingleton<IDbContextFactory<ApplicationDbContext>>(dbFactory);
        Services.AddScoped<DashboardMetricsQuery>();
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
    public async Task Render_WhenMerchantProfileMissing_DisplaysAlertWarning()
    {
        // 1. Arrange
        using var dbFactory = await SetupPageDependenciesAsync();
        AuthorizeAsMerchant(OrphanMerchantId, "orphan@test.com");

        // 2. Act
        var cut = Render<DashboardPage>();

        // 3. Assert
        var warning = cut.Find(".alert-warning");
        Assert.Contains("Store profile not found", warning.TextContent);
    }

    [Fact]
    public async Task Render_WhenValidMerchant_DisplaysStoreBannerAndFourOperationalKpis()
    {
        // 1. Arrange
        using var dbFactory = await SetupPageDependenciesAsync();
        AuthorizeAsMerchant(SarahMerchantId, "sarah@test.com");

        // 2. Act
        var cut = Render<DashboardPage>();

        // 3. Assert: Store banner and initial letter avatar "A"
        Assert.Contains("Artisan Brew Coffee", cut.Find("h2").TextContent);
        Assert.Contains("Specialty single-origin pour-over coffee", cut.Markup);
        Assert.Equal("A", cut.Find(".rounded-circle.bg-primary").TextContent.Trim());

        // Verify the 4 KPI cards: Active Campaigns (1 / 2), Active Customers (2), Stamps Issued (13), Rewards Redeemed (1)
        var kpiCards = cut.FindAll(".row.g-3 h3").Select(h => h.TextContent.Trim()).ToList();
        Assert.Contains("1", kpiCards[0]);
        Assert.Contains("/ 2", kpiCards[0]);
        Assert.Equal("2", kpiCards[1]);
        Assert.Equal("13", kpiCards[2]);
        Assert.Equal("1", kpiCards[3]);
    }

    [Fact]
    public async Task Render_WhenRecentCampaignsExist_DisplaysTableOrderedNewestFirstWithEditLinks()
    {
        // 1. Arrange
        using var dbFactory = await SetupPageDependenciesAsync();
        AuthorizeAsMerchant(SarahMerchantId, "sarah@test.com");

        // 2. Act
        var cut = Render<DashboardPage>();

        // 3. Assert: 2 rows ordered newest first
        var rows = cut.FindAll("tbody tr");
        Assert.Equal(2, rows.Count);
        Assert.Contains("Daily Espresso Club", rows[0].TextContent);
        Assert.Contains("Active", rows[0].TextContent);
        Assert.Contains("Weekend Roaster Reserve", rows[1].TextContent);
        Assert.Contains("Inactive", rows[1].TextContent);

        // Verify quick action and edit links exist
        Assert.NotEmpty(cut.FindAll("a[href='/merchant/campaigns/edit/1']"));
        Assert.NotEmpty(cut.FindAll("a[href='/merchant/issue-stamp']"));
    }
}