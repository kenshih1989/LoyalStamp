using Bunit;
using LoyalStamp.Data;
using LoyalStamp.Data.Entities;
using LoyalStamp.Features.Campaigns.CreateCampaign;
using LoyalStamp.Tests.TestHelpers;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;

namespace LoyalStamp.Tests.Components.Features.Campaigns;

public class CreateCampaignPageTests : BunitContext
{
    private const string SarahUserId = "200000000001";
    private const string OrphanUserId = "200000000099";

    // Helper: Registers TestDbContextFactory + CreateCampaignHandler in bUnit DI and seeds Merchant data
    private async Task<TestDbContextFactory> SetupPageDependenciesAsync()
    {
        var dbFactory = new TestDbContextFactory();
        await using (var db = dbFactory.CreateDbContext())
        {
            db.Users.AddRange(
                new User { Id = SarahUserId, UserName = "sarah@test.com", Email = "sarah@test.com", FullName = "Sarah Lim" },
                new User { Id = OrphanUserId, UserName = "orphan@test.com", Email = "orphan@test.com", FullName = "Orphan User" }
            );
            db.Merchants.Add(new Merchant { Id = 1, UserId = SarahUserId, StoreName = "Artisan Brew Coffee" });
            await db.SaveChangesAsync();
        }

        Services.AddSingleton<IDbContextFactory<ApplicationDbContext>>(dbFactory);
        Services.AddScoped<CreateCampaignHandler>();
        return dbFactory;
    }

    private void AuthorizeAsUser(string userId, string email)
    {
        var authContext = this.AddAuthorization();
        authContext.SetAuthorized(email);
        authContext.SetRoles("Merchant");
        authContext.SetClaims(new Claim(ClaimTypes.NameIdentifier, userId));
    }

    [Fact]
    public async Task SubmitForm_WithEmptyRequiredFields_ShowsValidationErrorsAndDoesNotNavigate()
    {
        // 1. Arrange
        using var dbFactory = await SetupPageDependenciesAsync();
        AuthorizeAsUser(SarahUserId, "sarah@test.com");

        var cut = Render<CreateCampaignPage>();
        var navManager = Services.GetRequiredService<NavigationManager>();

        // 2. Act: submit form with default empty Title and RewardItem
        cut.Find("form").Submit();

        // 3. Assert: DataAnnotationsValidator blocks submission and displays field validation messages
        var validationMessages = cut.FindAll(".text-danger.small");
        Assert.NotEmpty(validationMessages);
        Assert.Contains(validationMessages, m => m.TextContent.Contains("Campaign title is required."));
        Assert.Contains(validationMessages, m => m.TextContent.Contains("Reward item description is required."));

        // Ensure no database record was inserted and page stayed on current route
        await using var verifyDb = dbFactory.CreateDbContext();
        Assert.Empty(await verifyDb.StampCampaigns.ToListAsync());
        Assert.DoesNotContain("/merchant/campaigns", navManager.Uri);
    }

    [Fact]
    public async Task InputChange_WhenUpdatingSpendTargetAndReward_UpdatesLiveRulePreview()
    {
        // 1. Arrange
        using var dbFactory = await SetupPageDependenciesAsync();
        AuthorizeAsUser(SarahUserId, "sarah@test.com");

        var cut = Render<CreateCampaignPage>();

        // 2. Act: simulate merchant typing values into inputs
        //var textInputs = cut.FindAll("input.form-control");
        //var numberInputs = cut.FindAll("input[type='number']");

        cut.FindAll("input[type='number']")[0].Change("6.50");          // MinSpendAmount
        cut.FindAll("input[type='number']")[1].Change("12");            // TargetStamps
        cut.Find("input[placeholder*='Latte']").Change("1 Free Pour-Over"); // RewardItem (index 0 is Title, index 1 is RewardItem)

        // 3. Assert: verify the live rule preview alert dynamically reflects typed inputs
        var previewText = cut.Find(".alert-light").TextContent;
        Assert.Contains("$6.50", previewText);
        Assert.Contains("12", previewText);
        Assert.Contains("1 Free Pour-Over", previewText);
    }

    [Fact]
    public async Task SubmitForm_WithValidInputs_SavesCampaignAndNavigatesToCampaignList()
    {
        // 1. Arrange
        using var dbFactory = await SetupPageDependenciesAsync();
        AuthorizeAsUser(SarahUserId, "sarah@test.com");

        var cut = Render<CreateCampaignPage>();
        var navManager = Services.GetRequiredService<NavigationManager>();

        // 2. Act: fill in valid campaign inputs and submit
        cut.Find("#_request_Title").Change("Morning Espresso Club");
        cut.Find("#_request_MinSpendAmount").Change("5.00");
        cut.Find("#_request_TargetStamps").Change("8");
        cut.Find("#_request_RewardItem").Change("1 Free Latte");

        await cut.Find("form").SubmitAsync();

        // 3. Assert: verify campaign persisted in SQLite and page navigated to /merchant/campaigns
        await using var verifyDb = dbFactory.CreateDbContext();
        var saved = await verifyDb.StampCampaigns.SingleAsync();
        Assert.Equal("Morning Espresso Club", saved.Title);
        Assert.Equal(5.00m, saved.MinSpendAmount);
        Assert.Equal(8, saved.TargetStamps);
        Assert.Equal("1 Free Latte", saved.RewardItem);

        Assert.EndsWith("/merchant/campaigns", navManager.Uri);
    }

    [Fact]
    public async Task SubmitForm_WhenMerchantStoreProfileMissing_DisplaysAlertDangerBanner()
    {
        // 1. Arrange: logged in as user with Merchant role but no Merchants table record
        using var dbFactory = await SetupPageDependenciesAsync();
        AuthorizeAsUser(OrphanUserId, "orphan@test.com");

        var cut = Render<CreateCampaignPage>();

        // 2. Act: fill valid form fields and submit
        cut.Find("#_request_Title").Change("Orphan Campaign");
        cut.Find("#_request_RewardItem").Change("Free Coffee");

        await cut.Find("form").SubmitAsync();

        // 3. Assert: verify server-side handler error renders inside .alert-danger
        var errorAlert = cut.Find(".alert-danger");
        Assert.Contains("Store profile not found", errorAlert.TextContent);
    }
}