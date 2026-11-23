using Bunit;
using LoyalStamp.Data;
using LoyalStamp.Data.Entities;
using LoyalStamp.Features.StampOperations.IssueStamp;
using LoyalStamp.Features.StampOperations.RedeemReward;
using LoyalStamp.Shared;
using LoyalStamp.Tests.TestHelpers;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Security.Claims;

namespace LoyalStamp.Tests.Components.Features.StampOperations;

public class IssueStampPageTests : BunitContext
{
    private const string SarahMerchantId = "200000000001";
    private const string JasonMerchantId = "200000000002";
    private const string AlexCustomerId = "100000000001";

    private async Task<(TestDbContextFactory DbFactory, Guid SarahCardId, Guid JasonCardId)> SetupPageDependenciesAsync(
        int initialSarahCardStamps = 2)
    {
        var dbFactory = new TestDbContextFactory();
        var sarahCardId = Guid.NewGuid();
        var jasonCardId = Guid.NewGuid();

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
                    MerchantId = 2,
                    Title = "Boba Pass",
                    MinSpendAmount = 6.00m,
                    TargetStamps = 8,
                    RewardItem = "1 Free Boba",
                    IsActive = true
                }
            );
            db.CustomerCards.AddRange(
                new CustomerCard
                {
                    Id = sarahCardId,
                    UserId = AlexCustomerId,
                    CampaignId = 1,
                    CardCode = "LS-8A92B",
                    CurrentStamps = initialSarahCardStamps,
                    RowVersion = [1]
                },
                new CustomerCard
                {
                    Id = jasonCardId,
                    UserId = AlexCustomerId,
                    CampaignId = 2,
                    CardCode = "LS-BOBA1",
                    CurrentStamps = 3,
                    RowVersion = [1]
                }
            );
            await db.SaveChangesAsync();
        }

        // Mock SignalR HubContext so Issue/Redeem commands succeed cleanly in memory
        var mockHub = new Mock<IHubContext<StampNotificationHub>>();
        var mockClients = new Mock<IHubClients>();
        var mockClientProxy = new Mock<IClientProxy>();
        mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(mockClientProxy.Object);
        mockHub.Setup(h => h.Clients).Returns(mockClients.Object);

        Services.AddSingleton<IDbContextFactory<ApplicationDbContext>>(dbFactory);
        Services.AddSingleton(mockHub.Object);
        Services.AddScoped<IssueStampCommand>();
        Services.AddScoped<RedeemRewardCommand>();

        return (dbFactory, sarahCardId, jasonCardId);
    }

    private void AuthorizeAsMerchant(string userId, string email)
    {
        var authContext = this.AddAuthorization();
        authContext.SetAuthorized(email);
        authContext.SetRoles("Merchant");
        authContext.SetClaims(new Claim(ClaimTypes.NameIdentifier, userId));
    }

    [Fact]
    public async Task LookupCard_WithValidFallbackCode_DisplaysCardSummaryAndLiveStampPreview()
    {
        // 1. Arrange
        var (dbFactory, _, _) = await SetupPageDependenciesAsync(initialSarahCardStamps: 2);
        using var _ = dbFactory;
        AuthorizeAsMerchant(SarahMerchantId, "sarah@test.com");

        var cut = Render<IssueStampPage>();

        // 2. Act: enter short card code and click "Find Card"
        cut.Find("input[placeholder*='Scan QR GUID']").Input("ls-8a92b");
        await cut.Find(".input-group button.btn-primary").ClickAsync();

        // 3. Assert: card summary renders with customer details and locked redeem panel
        Assert.Contains("Alex Tan", cut.Markup);
        Assert.Contains("Daily Espresso Club", cut.Markup);
        Assert.Contains("Not Enough Stamps Yet", cut.Markup);

        // Act 2: type $18.50 into spend input and verify live preview shows 3 stamps (Floor(18.50 / 5.00))
        cut.Find("input[type='number']").Input("18.50");
        Assert.Contains("Preview: 3 stamp(s) will be issued", cut.Find(".form-text").TextContent);
    }

    [Fact]
    public async Task LookupCard_WhenCardBelongsToAnotherMerchant_DisplaysAlertDangerBanner()
    {
        // 1. Arrange: Sarah tries to look up Jason's customer card (LS-BOBA1)
        var (dbFactory, _, _) = await SetupPageDependenciesAsync();
        using var _ = dbFactory;
        AuthorizeAsMerchant(SarahMerchantId, "sarah@test.com");

        var cut = Render<IssueStampPage>();

        // 2. Act
        cut.Find("input[placeholder*='Scan QR GUID']").Input("LS-BOBA1");
        await cut.Find(".input-group button.btn-primary").ClickAsync();

        // 3. Assert
        var errorAlert = cut.Find(".alert-danger");
        Assert.Contains("does not belong to your store", errorAlert.TextContent);
        Assert.DoesNotContain("Alex Tan", cut.Markup);
    }

    [Fact]
    public async Task IssueStamps_WhenValidSpendSubmitted_PersistsStampsAndUnlocksRedemptionPanel()
    {
        // 1. Arrange: card starts with 2 / 8 stamps
        var (dbFactory, sarahCardId, _) = await SetupPageDependenciesAsync(initialSarahCardStamps: 2);
        using var _ = dbFactory;
        AuthorizeAsMerchant(SarahMerchantId, "sarah@test.com");

        var cut = Render<IssueStampPage>();
        cut.Find("input[placeholder*='Scan QR GUID']").Input("LS-8A92B");
        await cut.Find(".input-group button.btn-primary").ClickAsync();

        // 2. Act: enter $35.00 (7 stamps -> 2 + 7 = 9 / 8) and click Issue button
        cut.Find("input[type='number']").Input("35.00");
        cut.Find("input[placeholder*='Receipt']").Change("Receipt #501");
        await cut.Find("button.btn-primary.w-100").ClickAsync();

        // 3. Assert: success banner appears, balance updates to 9 / 8, and Redeem button unlocks
        Assert.Contains("Issued 7 stamp(s)!", cut.Find(".alert-success").TextContent);
        Assert.Contains("Eligible for Redemption!", cut.Markup);
        Assert.NotEmpty(cut.FindAll("button.btn-success.w-100"));

        await using var verifyDb = dbFactory.CreateDbContext();
        var updatedCard = await verifyDb.CustomerCards.FindAsync(sarahCardId);
        Assert.Equal(9, updatedCard!.CurrentStamps);
    }

    [Fact]
    public async Task RedeemReward_WhenCardHasOverflowStamps_DeductsTargetAndPreservesCarryover()
    {
        // 1. Arrange: card starts eligible with 9 / 8 stamps (1 carryover)
        var (dbFactory, sarahCardId, _) = await SetupPageDependenciesAsync(initialSarahCardStamps: 9);
        using var _ = dbFactory;
        AuthorizeAsMerchant(SarahMerchantId, "sarah@test.com");

        var cut = Render<IssueStampPage>();
        cut.Find("input[placeholder*='Scan QR GUID']").Input("LS-8A92B");
        await cut.Find(".input-group button.btn-primary").ClickAsync();

        // 2. Act: click "Confirm Redemption (-8 Stamps)"
        await cut.Find("button.btn-success.w-100").ClickAsync();

        // 3. Assert: success banner appears and carryover balance 1 / 8 is preserved
        Assert.Contains("Carryover balance: 1 stamp(s).", cut.Find(".alert-success").TextContent);
        Assert.Contains("Not Enough Stamps Yet", cut.Markup);

        await using var verifyDb = dbFactory.CreateDbContext();
        var updatedCard = await verifyDb.CustomerCards.FindAsync(sarahCardId);
        Assert.Equal(1, updatedCard!.CurrentStamps);
        Assert.Equal(1, updatedCard.TotalRedeemedCount);
    }
}