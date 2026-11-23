using Bunit;
using LoyalStamp.Data;
using LoyalStamp.Data.Entities;
using LoyalStamp.Features.CustomerWallet.MyCards;
using LoyalStamp.Tests.TestHelpers;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Security.Claims;

namespace LoyalStamp.Tests.Components.Features.CustomerWallet;

public class MyWalletPageTests : BunitContext
{
    private const string SarahMerchantId = "200000000001";
    private const string AlexCustomerId = "100000000001";

    private async Task<TestDbContextFactory> SetupPageDependenciesAsync(bool seedCard = true)
    {
        var dbFactory = new TestDbContextFactory();
        await using (var db = dbFactory.CreateDbContext())
        {
            db.Users.AddRange(
                new User { Id = SarahMerchantId, UserName = "sarah@test.com", Email = "sarah@test.com", FullName = "Sarah Lim" },
                new User { Id = AlexCustomerId, UserName = "alex@test.com", Email = "alex@test.com", FullName = "Alex Tan" }
            );
            db.Merchants.Add(new Merchant { Id = 1, UserId = SarahMerchantId, StoreName = "Artisan Brew Coffee" });
            db.StampCampaigns.Add(new StampCampaign
            {
                Id = 1,
                MerchantId = 1,
                Title = "Daily Espresso Club",
                MinSpendAmount = 5.00m,
                TargetStamps = 8,
                RewardItem = "1 Free Latte",
                IsActive = true
            });

            if (seedCard)
            {
                db.CustomerCards.Add(new CustomerCard
                {
                    Id = Guid.NewGuid(),
                    UserId = AlexCustomerId,
                    CampaignId = 1,
                    CardCode = "LS-8A92B",
                    CurrentStamps = 9, // 9 / 8 -> 1 carryover overflow stamp!
                    TotalRedeemedCount = 0,
                    RowVersion = [1]
                });
            }

            await db.SaveChangesAsync();
        }

        var mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        Services.AddSingleton<IDbContextFactory<ApplicationDbContext>>(dbFactory);
        Services.AddSingleton(mockHttpContextAccessor.Object);
        Services.AddScoped<MyCardsQuery>();
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
    public async Task Render_WhenWalletIsEmpty_ShowsEmptyPromptAndExploreLink()
    {
        // 1. Arrange
        using var dbFactory = await SetupPageDependenciesAsync(seedCard: false);
        AuthorizeAsCustomer(AlexCustomerId);

        // 2. Act
        var cut = Render<MyWalletPage>();

        // 3. Assert
        Assert.Contains("Your Wallet is Empty", cut.Find(".card-body h5").TextContent);
        Assert.NotEmpty(cut.FindAll("a[href='/explore']"));
    }

    [Fact]
    public async Task Render_WhenCardHasOverflowStamps_ShowsRewardReadyBadgeAndCarryoverSlot()
    {
        // 1. Arrange: card has 9 stamps for an 8-stamp target
        using var dbFactory = await SetupPageDependenciesAsync(seedCard: true);
        AuthorizeAsCustomer(AlexCustomerId);

        // 2. Act
        var cut = Render<MyWalletPage>();

        // 3. Assert: "Reward Ready!" badge and 9 total visual stamp circles (including 1 yellow overflow slot)
        Assert.Contains("Reward Ready!", cut.Find(".badge.bg-success").TextContent);
        Assert.Equal(9, cut.FindAll(".rounded-circle.border").Count);
        Assert.Single(cut.FindAll(".bg-warning"));
    }

    [Fact]
    public async Task ClickCard_WhenClicked_OpensCardQrModalWithBase64QrAndClosesOnDismiss()
    {
        // 1. Arrange
        using var dbFactory = await SetupPageDependenciesAsync(seedCard: true);
        AuthorizeAsCustomer(AlexCustomerId);

        var cut = Render<MyWalletPage>();

        // Wait until loading spinner finishes and the stamp card is rendered
        cut.WaitForAssertion(() =>
        {
            Assert.True(cut.FindAll("div.card.h-100").Count > 0, $"Expected card element, but rendered markup was:\n{cut.Markup}");
        });
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("div.card.h-100")));
        Assert.Empty(cut.FindAll(".modal.show"));

        // 2. Act: click the stamp card to open CardQrModal
        //await cut.Find("div[style*='cursor: pointer']").ClickAsync();
        await cut.Find("div.card.h-100").ClickAsync();


        // 3. Assert: modal renders Base64 QR image, fallback code LS-8A92B, and reward ready alert
        var modal = cut.Find(".modal.show");
        var qrImg = modal.QuerySelector("img[alt='Checkout QR Code']")!;
        Assert.StartsWith("data:image/png;base64,", qrImg.GetAttribute("src"));
        Assert.Contains("LS-8A92B", modal.TextContent);
        Assert.Contains("Ready to redeem: 1 Free Latte!", modal.TextContent);

        // Act: close modal via footer Close button
        await modal.QuerySelector(".modal-footer button")!.ClickAsync();
        Assert.Empty(cut.FindAll(".modal.show"));
    }
}