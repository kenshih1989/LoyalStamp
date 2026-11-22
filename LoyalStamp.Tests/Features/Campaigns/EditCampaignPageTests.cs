using Bunit;
using LoyalStamp.Data;
using LoyalStamp.Data.Entities;
using LoyalStamp.Features.Campaigns.EditCampaign;
using LoyalStamp.Tests.TestHelpers;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;

namespace LoyalStamp.Tests.Features.Campaigns;

public class EditCampaignPageTests : BunitContext
{
    private const string SarahUserId = "200000000001";
    private const string JasonUserId = "200000000002";

    private async Task<TestDbContextFactory> SetupPageDependenciesAsync()
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
            db.StampCampaigns.Add(new StampCampaign
            {
                Id = 20,
                MerchantId = 1, // Belongs to Sarah
                Title = "Original Espresso Club",
                MinSpendAmount = 5.00m,
                TargetStamps = 8,
                RewardItem = "1 Free Latte",
                IsActive = true
            });
            await db.SaveChangesAsync();
        }

        Services.AddSingleton<IDbContextFactory<ApplicationDbContext>>(dbFactory);
        Services.AddScoped<EditCampaignHandler>();
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
    public async Task Render_WhenCampaignBelongsToMerchant_PreFillsEditFormFields()
    {
        // 1. Arrange
        using var dbFactory = await SetupPageDependenciesAsync();
        AuthorizeAsMerchant(SarahUserId, "sarah@test.com");

        // 2. Act: pass route parameter CampaignId = 20
        var cut = Render<EditCampaignPage>(parameters => parameters
            .Add(p => p.CampaignId, 20));

        // 3. Assert: verify form inputs are populated with existing campaign values
        Assert.Equal("Original Espresso Club", cut.Find("#_request_Title").GetAttribute("value"));
        Assert.Equal("5", cut.Find("#_request_MinSpendAmount").GetAttribute("value"));
        Assert.Equal("8", cut.Find("#_request_TargetStamps").GetAttribute("value"));
        Assert.Equal("1 Free Latte", cut.Find("#_request_RewardItem").GetAttribute("value"));
        Assert.True(cut.Find("#editIsActiveSwitch").HasAttribute("checked"));
    }

    [Fact]
    public async Task Render_WhenCampaignBelongsToAnotherMerchant_ShowsAccessWarningAndHidesForm()
    {
        // 1. Arrange: Jason tries to access Sarah's CampaignId = 20
        using var dbFactory = await SetupPageDependenciesAsync();
        AuthorizeAsMerchant(JasonUserId, "jason@test.com");

        // 2. Act
        var cut = Render<EditCampaignPage>(parameters => parameters
            .Add(p => p.CampaignId, 20));

        // 3. Assert
        var warningAlert = cut.Find(".alert-warning");
        Assert.Contains("Campaign not found or you do not have access", warningAlert.TextContent);
        Assert.Empty(cut.FindAll("form"));
    }

    [Fact]
    public async Task SubmitForm_WithClearedRequiredTitle_ShowsValidationMessageAndDoesNotSave()
    {
        // 1. Arrange
        using var dbFactory = await SetupPageDependenciesAsync();
        AuthorizeAsMerchant(SarahUserId, "sarah@test.com");

        var cut = Render<EditCampaignPage>(parameters => parameters
            .Add(p => p.CampaignId, 20));
        var navManager = Services.GetRequiredService<NavigationManager>();

        // 2. Act: clear required Title field and submit
        cut.Find("#_request_Title").Change("");
        await cut.Find("form").SubmitAsync();

        // 3. Assert
        var messages = cut.FindAll(".text-danger.small");
        Assert.Contains(messages, m => m.TextContent.Contains("Campaign title is required."));
        Assert.DoesNotContain("/merchant/campaigns", navManager.Uri);
    }

    [Fact]
    public async Task SubmitForm_WithValidUpdates_PersistsChangesAndNavigatesToCampaignList()
    {
        // 1. Arrange
        using var dbFactory = await SetupPageDependenciesAsync();
        AuthorizeAsMerchant(SarahUserId, "sarah@test.com");

        var cut = Render<EditCampaignPage>(parameters => parameters
            .Add(p => p.CampaignId, 20));
        var navManager = Services.GetRequiredService<NavigationManager>();

        // 2. Act: update title, min spend, target stamps, reward, and toggle IsActive off
        cut.Find("#_request_Title").Change("Updated Roaster Club");
        cut.Find("#_request_MinSpendAmount").Change("7.50");
        cut.Find("#_request_TargetStamps").Change("10");
        cut.Find("#_request_RewardItem").Change("1 Free Pour-Over");
        cut.Find("#editIsActiveSwitch").Change(false);

        await cut.Find("form").SubmitAsync();

        // 3. Assert: verify database updated and navigated back to /merchant/campaigns
        await using var verifyDb = dbFactory.CreateDbContext();
        var updated = await verifyDb.StampCampaigns.FindAsync(20);
        Assert.Equal("Updated Roaster Club", updated!.Title);
        Assert.Equal(7.50m, updated.MinSpendAmount);
        Assert.Equal(10, updated.TargetStamps);
        Assert.Equal("1 Free Pour-Over", updated.RewardItem);
        Assert.False(updated.IsActive);

        Assert.EndsWith("/merchant/campaigns", navManager.Uri);
    }
}