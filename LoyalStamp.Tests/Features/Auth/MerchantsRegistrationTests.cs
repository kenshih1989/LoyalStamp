using LoyalStamp.Data;
using LoyalStamp.Data.Entities;
using LoyalStamp.Features.Auth.MerchantRegistration;
using LoyalStamp.Shared;
using LoyalStamp.Tests.TestHelpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace LoyalStamp.Tests.Features.Auth;

public class MerchantsRegistrationTests
{
    private static async Task<(TestDbContextFactory DbFactory, UserManager<User> UserManager, Mock<UserIdGenerator> MockIdGenerator)>
        SetupScenarioAsync(string generatedMerchantId = "200000000001")
    {
        var dbFactory = new TestDbContextFactory();
        var setupDb = dbFactory.CreateDbContext();

        var roleStore = new RoleStore<IdentityRole>(setupDb);
        await roleStore.CreateAsync(new IdentityRole { Name = "Customer", NormalizedName = "CUSTOMER" });
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
        mockIdGen.Setup(g => g.GenerateMerchantIdAsync()).ReturnsAsync(generatedMerchantId);

        return (dbFactory, userManager, mockIdGen);
    }

    [Fact]
    public async Task HandleAsync_ValidRequest_CreatesMerchantUserRoleAndOneToOneStoreProfile()
    {
        var (dbFactory, userManager, mockIdGen) = await SetupScenarioAsync("200000000001");
        using var _ = dbFactory;
        var handler = new RegisterMerchantHandler(userManager, mockIdGen.Object, dbFactory);

        var request = new RegisterMerchantRequest
        {
            FullName = "Sarah Lim",
            Email = "sarah@artisanbrew.com",
            Password = "Password123",
            ConfirmPassword = "Password123",
            StoreName = "  Artisan Brew Coffee  ",
            Description = "  Specialty pour-over coffee  ",
            LogoUrl = "https://placehold.co/200x200"
        };

        var (succeeded, errors) = await handler.HandleAsync(request);

        Assert.True(succeeded);
        Assert.Empty(errors);

        await using var verifyDb = dbFactory.CreateDbContext();
        var createdUser = await verifyDb.Users.SingleAsync();
        Assert.Equal("200000000001", createdUser.Id);
        Assert.True(await userManager.IsInRoleAsync(createdUser, "Merchant"));

        var createdStore = await verifyDb.Merchants.SingleAsync();
        Assert.Equal("200000000001", createdStore.UserId);
        Assert.Equal("Artisan Brew Coffee", createdStore.StoreName);
        Assert.Equal("Specialty pour-over coffee", createdStore.Description);
        Assert.Equal("https://placehold.co/200x200", createdStore.LogoUrl);
    }

    [Fact]
    public async Task HandleAsync_OptionalFieldsBlank_SavesNullForDescriptionAndLogoUrl()
    {
        var (dbFactory, userManager, mockIdGen) = await SetupScenarioAsync("200000000002");
        using var _ = dbFactory;
        var handler = new RegisterMerchantHandler(userManager, mockIdGen.Object, dbFactory);

        var request = new RegisterMerchantRequest
        {
            FullName = "Rachel Goh",
            Email = "rachel@crustcrumb.com",
            Password = "Password123",
            ConfirmPassword = "Password123",
            StoreName = "Crust & Crumb Bakery",
            Description = "   ",
            LogoUrl = ""
        };

        var (succeeded, errors) = await handler.HandleAsync(request);

        Assert.True(succeeded);
        Assert.Empty(errors);

        await using var verifyDb = dbFactory.CreateDbContext();
        var store = await verifyDb.Merchants.SingleAsync();
        Assert.Null(store.Description);
        Assert.Null(store.LogoUrl);
    }

    [Fact]
    public async Task HandleAsync_DuplicateBusinessEmail_ReturnsFailureWithoutCreatingOrphanStore()
    {
        var (dbFactory, userManager, mockIdGen) = await SetupScenarioAsync("200000000001");
        using var _ = dbFactory;
        var handler = new RegisterMerchantHandler(userManager, mockIdGen.Object, dbFactory);

        var firstRequest = new RegisterMerchantRequest
        {
            FullName = "Sarah Lim",
            Email = "sarah@artisanbrew.com",
            Password = "Password123",
            ConfirmPassword = "Password123",
            StoreName = "Artisan Brew Coffee"
        };
        await handler.HandleAsync(firstRequest);

        mockIdGen.Setup(g => g.GenerateMerchantIdAsync()).ReturnsAsync("200000000002");
        var duplicateRequest = new RegisterMerchantRequest
        {
            FullName = "Fake Owner",
            Email = "sarah@artisanbrew.com",
            Password = "Password123",
            ConfirmPassword = "Password123",
            StoreName = "Fake Store"
        };

        var (succeeded, errors) = await handler.HandleAsync(duplicateRequest);

        Assert.False(succeeded);
        Assert.NotEmpty(errors);

        await using var verifyDb = dbFactory.CreateDbContext();
        Assert.Equal(1, await verifyDb.Users.CountAsync());
        Assert.Equal(1, await verifyDb.Merchants.CountAsync());
    }
}