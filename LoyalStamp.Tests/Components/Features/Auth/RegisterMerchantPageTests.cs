using Bunit;
using LoyalStamp.Data;
using LoyalStamp.Data.Entities;
using LoyalStamp.Features.Auth.MerchantRegistration;
using LoyalStamp.Shared;
using LoyalStamp.Tests.TestHelpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace LoyalStamp.Tests.Components.Features.Auth;

public class RegisterMerchantPageTests : BunitContext
{
    private async Task<TestDbContextFactory> SetupPageDependenciesAsync()
    {
        var dbFactory = new TestDbContextFactory();
        var setupDb = dbFactory.CreateDbContext();

        var roleStore = new RoleStore<IdentityRole>(setupDb);
        await roleStore.CreateAsync(new IdentityRole { Name = "Merchant", NormalizedName = "MERCHANT" });

        var userStore = new UserStore<User, IdentityRole, ApplicationDbContext>(setupDb);
        var identityOptions = Options.Create(new IdentityOptions
        {
            User = { RequireUniqueEmail = true },
            Password = { RequiredLength = 6, RequireNonAlphanumeric = false }
        });

        var userManager = new UserManager<User>(
            userStore,
            identityOptions,
            new PasswordHasher<User>(),
            [new UserValidator<User>()],
            [new PasswordValidator<User>()],
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            services: null!,
            NullLogger<UserManager<User>>.Instance);

        var mockIdGen = new Mock<UserIdGenerator>(dbFactory);
        mockIdGen.Setup(g => g.GenerateMerchantIdAsync()).ReturnsAsync("200000000001");

        Services.AddSingleton<IDbContextFactory<ApplicationDbContext>>(dbFactory);
        Services.AddSingleton(userManager);
        Services.AddSingleton(mockIdGen.Object);
        Services.AddScoped<RegisterMerchantHandler>();

        return dbFactory;
    }

    [Fact]
    public async Task SubmitForm_WithInvalidLogoUrlAndMissingStoreName_ShowsValidationErrors()
    {
        // 1. Arrange
        using var dbFactory = await SetupPageDependenciesAsync();
        var cut = Render<RegisterMerchantPage>();

        // 2. Act: enter invalid URL in LogoUrl (last input) and submit with empty required fields
        cut.FindAll("input.form-control")[5].Change("not-a-valid-url"); // LogoUrl
        await cut.Find("form").SubmitAsync();

        // 3. Assert
        cut.WaitForAssertion(() =>
        {
            var messages = cut.FindAll(".text-danger.small");
            Assert.Contains(messages, m => m.TextContent.Contains("Owner full name is required."));
            Assert.Contains(messages, m => m.TextContent.Contains("Store name is required."));
            Assert.Contains(messages, m => m.TextContent.Contains("Please enter a valid URL"));
        });
    }

    [Fact]
    public async Task SubmitForm_WithValidAccountAndStoreInputs_CreatesMerchantAndNavigatesToLogin()
    {
        // 1. Arrange
        using var dbFactory = await SetupPageDependenciesAsync();
        var cut = Render<RegisterMerchantPage>();
        var navManager = Services.GetRequiredService<NavigationManager>();

        // 2. Act: query each input fresh right before calling .Change()
        //cut.Find("input[placeholder='e.g. Sarah Lim']").Change("Sarah Lim");                  // FullName (or match your exact placeholder)
        //cut.Find("input[type='email']").Change("sarah@artisanbrew.com");                    // Email
        //cut.FindAll("input[type='password']")[0].Change("Test@1234");                       // Password
        //cut.FindAll("input[type='password']")[1].Change("Test@1234");                       // ConfirmPassword
        //cut.Find("input[placeholder='e.g. Artisan Brew Coffee']").Change("Artisan Brew Coffee"); // StoreName
        //cut.Find("textarea.form-control").Change("Specialty coffee");                       // Description
        //cut.Find("input[placeholder*='http']").Change("https://placehold.co/200");          // LogoUrl

        // 2. Act: inline FindAll re-queries the updated render tree after every re-render
        cut.FindAll("input.form-control")[0].Change("Sarah Lim");               // FullName
        cut.FindAll("input.form-control")[1].Change("sarah@artisanbrew.com");   // Email
        cut.FindAll("input.form-control")[2].Change("Test@1234");               // Password
        cut.FindAll("input.form-control")[3].Change("Test@1234");               // ConfirmPassword
        cut.FindAll("input.form-control")[4].Change("Artisan Brew Coffee");     // StoreName
        cut.Find("textarea.form-control").Change("Specialty coffee");           // Description
        cut.FindAll("input.form-control")[5].Change("https://placehold.co/200");// LogoUrl

        cut.Find("form").Submit();

        // 3. Assert: verify both User and 1:1 Merchant store record persisted
        await using var verifyDb = dbFactory.CreateDbContext();
        var user = await verifyDb.Users.SingleAsync();
        var store = await verifyDb.Merchants.SingleAsync();

        Assert.Equal("200000000001", user.Id);
        Assert.Equal("200000000001", store.UserId);
        Assert.Equal("Artisan Brew Coffee", store.StoreName);
        Assert.EndsWith("/login?registered=true", navManager.Uri);
    }
}