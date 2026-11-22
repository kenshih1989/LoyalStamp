using LoyalStamp.Data;
using LoyalStamp.Data.Entities;
using LoyalStamp.Features.Auth.UserRegistration;
using LoyalStamp.Shared;
using LoyalStamp.Tests.TestHelpers;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace LoyalStamp.Tests.Unit.Features.Auth;

public class UserRegistrationTests
{
    private static async Task<(TestDbContextFactory DbFactory, UserManager<User> UserManager, Mock<UserIdGenerator> MockIdGenerator)>
        SetupScenarioAsync(string generatedCustomerId = "100000000001")
    {
        var dbFactory = new TestDbContextFactory();
        var setupDb = dbFactory.CreateDbContext();

        // Seed required Identity roles
        var roleStore = new RoleStore<IdentityRole>(setupDb);
        await roleStore.CreateAsync(new IdentityRole { Name = "Customer", NormalizedName = "CUSTOMER" });
        await roleStore.CreateAsync(new IdentityRole { Name = "Merchant", NormalizedName = "MERCHANT" });

        // Build real UserManager backed by in-memory SQLite ApplicationDbContext
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

        // Mock UserIdGenerator (since SQLite does not support SQL Server SEQUENCE)
        var mockIdGen = new Mock<UserIdGenerator>(dbFactory);
        mockIdGen.Setup(g => g.GenerateCustomerIdAsync()).ReturnsAsync(generatedCustomerId);

        return (dbFactory, userManager, mockIdGen);
    }

    [Fact]
    public async Task HandleAsync_ValidRequest_CreatesCustomerUserWithCustomIdAndRole()
    {
        var (dbFactory, userManager, mockIdGen) = await SetupScenarioAsync("100000000001");
        using var _ = dbFactory;
        var handler = new RegisterUserHandler(userManager, mockIdGen.Object);

        var request = new RegisterUserRequest
        {
            FullName = "Alex Tan",
            Email = "alex.tan@example.com",
            Password = "Password123",
            ConfirmPassword = "Password123"
        };

        var (succeeded, errors) = await handler.HandleAsync(request);

        Assert.True(succeeded);
        Assert.Empty(errors);

        await using var verifyDb = dbFactory.CreateDbContext();
        var createdUser = await verifyDb.Users.SingleAsync();
        Assert.Equal("100000000001", createdUser.Id);
        Assert.Equal("Alex Tan", createdUser.FullName);
        Assert.Equal("alex.tan@example.com", createdUser.Email);
        Assert.True(await userManager.IsInRoleAsync(createdUser, "Customer"));
    }

    [Fact]
    public async Task HandleAsync_DuplicateEmail_ReturnsFailureWithoutCreatingDuplicateUser()
    {
        var (dbFactory, userManager, mockIdGen) = await SetupScenarioAsync("100000000001");
        using var _ = dbFactory;
        var handler = new RegisterUserHandler(userManager, mockIdGen.Object);

        var firstRequest = new RegisterUserRequest
        {
            FullName = "Alex Tan",
            Email = "alex.tan@example.com",
            Password = "Password123",
            ConfirmPassword = "Password123"
        };
        await handler.HandleAsync(firstRequest);

        mockIdGen.Setup(g => g.GenerateCustomerIdAsync()).ReturnsAsync("100000000002");
        var duplicateRequest = new RegisterUserRequest
        {
            FullName = "Alex Imposter",
            Email = "alex.tan@example.com",
            Password = "Password123",
            ConfirmPassword = "Password123"
        };

        var (succeeded, errors) = await handler.HandleAsync(duplicateRequest);

        Assert.False(succeeded);
        Assert.NotEmpty(errors);

        await using var verifyDb = dbFactory.CreateDbContext();
        Assert.Equal(1, await verifyDb.Users.CountAsync());
    }

    [Fact]
    public async Task HandleAsync_WeakPassword_ReturnsValidationErrors()
    {
        var (dbFactory, userManager, mockIdGen) = await SetupScenarioAsync();
        using var _ = dbFactory;
        var handler = new RegisterUserHandler(userManager, mockIdGen.Object);

        var request = new RegisterUserRequest
        {
            FullName = "Weak Pass User",
            Email = "weak@example.com",
            Password = "123", // Too short & missing uppercase/lowercase
            ConfirmPassword = "123"
        };

        var (succeeded, errors) = await handler.HandleAsync(request);

        Assert.False(succeeded);
        Assert.NotEmpty(errors);
    }
}