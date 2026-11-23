using Bunit;
using LoyalStamp.Data;
using LoyalStamp.Data.Entities;
using LoyalStamp.Features.HistoryAndDashboard.CustomerHistory;
using LoyalStamp.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;

namespace LoyalStamp.Tests.Components.Features.HistoryAndDashboard;

public class TransactionHistoryPageTests : BunitContext
{
    private const string SarahMerchantId = "200000000001";
    private const string JasonMerchantId = "200000000002";
    private const string AlexCustomerId = "100000000001";

    private async Task<TestDbContextFactory> SetupPageDependenciesAsync(bool seedTransactions = true)
    {
        var dbFactory = new TestDbContextFactory();
        await using (var db = dbFactory.CreateDbContext())
        {
            db.Users.AddRange(
                new User { Id = SarahMerchantId, UserName = "sarah@test.com", Email = "sarah@test.com", FullName = "Sarah Lim" },
                new User { Id = JasonMerchantId, UserName = "jason@test.com", Email = "jason@test.com", FullName = "Jason Ong" },
                new User { Id = AlexCustomerId, UserName = "alex@test.com", Email = "alex@test.com", FullName = "Alex Tan" }
            );
            db.Merchants.AddRange(
                new Merchant { Id = 1, UserId = SarahMerchantId, StoreName = "Artisan Brew Coffee" },
                new Merchant { Id = 2, UserId = JasonMerchantId, StoreName = "Boba Avenue" }
            );
            db.StampCampaigns.AddRange(
                new StampCampaign { Id = 1, MerchantId = 1, Title = "Espresso Club", MinSpendAmount = 5m, TargetStamps = 8, RewardItem = "Free Latte", IsActive = true },
                new StampCampaign { Id = 2, MerchantId = 2, Title = "Boba Pass", MinSpendAmount = 6m, TargetStamps = 8, RewardItem = "Free Boba", IsActive = true }
            );

            if (seedTransactions)
            {
                var card1 = Guid.NewGuid();
                var card2 = Guid.NewGuid();
                db.CustomerCards.AddRange(
                    new CustomerCard { Id = card1, UserId = AlexCustomerId, CampaignId = 1, CardCode = "LS-A0001", CurrentStamps = 1, TotalRedeemedCount = 1, RowVersion = [1] },
                    new CustomerCard { Id = card2, UserId = AlexCustomerId, CampaignId = 2, CardCode = "LS-A0002", CurrentStamps = 4, TotalRedeemedCount = 0, RowVersion = [1] }
                );

                var baseTime = new DateTime(2026, 11, 20, 10, 0, 0);
                db.StampTransactions.AddRange(
                    new StampTransaction { CustomerCardId = card1, TransactionType = "Earn", StampsChanged = 9, SpendAmount = 48.50m, Notes = "Coffee Order", CreatedAt = baseTime },
                    new StampTransaction { CustomerCardId = card1, TransactionType = "Redeem", StampsChanged = -8, SpendAmount = null, Notes = "Redeemed Latte", CreatedAt = baseTime.AddMinutes(5) },
                    new StampTransaction { CustomerCardId = card2, TransactionType = "Earn", StampsChanged = 4, SpendAmount = 25.00m, Notes = "Boba Order", CreatedAt = baseTime.AddMinutes(10) }
                );
            }

            await db.SaveChangesAsync();
        }

        Services.AddSingleton<IDbContextFactory<ApplicationDbContext>>(dbFactory);
        Services.AddScoped<HistoryQuery>();
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
    public async Task Render_WhenCustomerHasNoTransactions_ShowsEmptyHistoryCard()
    {
        // 1. Arrange
        using var dbFactory = await SetupPageDependenciesAsync(seedTransactions: false);
        AuthorizeAsCustomer(AlexCustomerId);

        // 2. Act
        var cut = Render<TransactionHistoryPage>();

        // 3. Assert
        Assert.Contains("No Transactions Yet", cut.Find(".card-body h5").TextContent);
        Assert.NotEmpty(cut.FindAll("a[href='/explore']"));
    }

    [Fact]
    public async Task Render_WhenTransactionsExist_DisplaysSummaryKpisAndTableRows()
    {
        // 1. Arrange
        using var dbFactory = await SetupPageDependenciesAsync(seedTransactions: true);
        AuthorizeAsCustomer(AlexCustomerId);

        // 2. Act
        var cut = Render<TransactionHistoryPage>();

        // 3. Assert: verify the 3 summary KPI cards (+13, 1, $73.50) and 3 table rows
        var kpiValues = cut.FindAll(".row.g-3 h3").Select(h => h.TextContent.Trim()).ToList();
        Assert.Equal(["+13", "1", "$73.50"], kpiValues);
        Assert.Equal(3, cut.FindAll("tbody tr").Count);
    }

    [Fact]
    public async Task FilterControls_WhenFilteringByRedeemTypeAndStore_UpdatesVisibleRows()
    {
        // 1. Arrange
        using var dbFactory = await SetupPageDependenciesAsync(seedTransactions: true);
        AuthorizeAsCustomer(AlexCustomerId);

        var cut = Render<TransactionHistoryPage>();

        // 2. Act 1: click "Rewards Redeemed (1)" button in the filter btn-group
        var filterButtons = cut.FindAll(".btn-group button");
        await filterButtons[2].ClickAsync(); // index 0 = All, 1 = Earn, 2 = Redeem

        // Assert 1: only the Redeem row (-8) remains
        var rowsAfterTypeFilter = cut.FindAll("tbody tr");
        Assert.Single(rowsAfterTypeFilter);
        Assert.Contains("-8", rowsAfterTypeFilter[0].TextContent);

        // 3. Act 2: reset type to "All" and filter store dropdown to "Boba Avenue"
        await filterButtons[0].ClickAsync();
        cut.Find("select.form-select").Change("Boba Avenue");

        // Assert 2: only Boba Avenue row (+4) remains
        var rowsAfterStoreFilter = cut.FindAll("tbody tr");
        Assert.Single(rowsAfterStoreFilter);
        Assert.Contains("Boba Avenue", rowsAfterStoreFilter[0].TextContent);
        Assert.Contains("+4", rowsAfterStoreFilter[0].TextContent);
    }
}