using Bunit;
using LoyalStamp.Data;
using LoyalStamp.Data.Entities;
using LoyalStamp.Features.Auth.UserRegistration;
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

namespace LoyalStamp.Tests.Features.Auth;

public class RegisterUserPageTests : BunitContext
{
    private async Task<TestDbContextFactory> SetupPageDependenciesAsync()
    {
        var dbFactory = new TestDbContextFactory();
        var setupDb = dbFactory.CreateDbContext();

        var roleStore = new RoleStore<IdentityRole>(setupDb);
        await roleStore.CreateAsync(new IdentityRole { Name = "Customer", NormalizedName = "CUSTOMER" });

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
        mockIdGen.Setup(g => g.GenerateCustomerIdAsync()).ReturnsAsync("100000000001");

        Services.AddSingleton(userManager);
        Services.AddSingleton(mockIdGen.Object);
        Services.AddScoped<RegisterUserHandler>();

        return dbFactory;
    }

    [Fact]
    public async Task SubmitForm_WithMismatchedPasswordsAndEmptyFields_ShowsValidationMessages()
    {
        // 1. Arrange
        using var dbFactory = await SetupPageDependenciesAsync();
        var cut = Render<RegisterUserPage>();
        var navManager = Services.GetRequiredService<NavigationManager>();

        // 2. Act: enter mismatched passwords and leave FullName/Email empty
        var inputs = cut.FindAll("input.form-control");
        cut.FindAll("input.form-control")[2].Change("Password123"); // Password
        cut.FindAll("input.form-control")[3].Change("Different999"); // ConfirmPassword
        //cut.Find("form").Submit();
        await cut.Find("form").SubmitAsync();

        // 3. Assert
        //var messages = cut.FindAll(".text-danger.small");
        //Assert.Contains(messages, m => m.TextContent.Contains("Full name is required."));
        //Assert.Contains(messages, m => m.TextContent.Contains("Email is required."));
        //Assert.Contains(messages, m => m.TextContent.Contains("Passwords do not match."));
        //Assert.DoesNotContain("/login", navManager.Uri);

        cut.WaitForAssertion(() =>
        {
            var messages = cut.FindAll(".text-danger.small");
            Assert.Contains(messages, m => m.TextContent.Contains("Full name is required."));
            Assert.Contains(messages, m => m.TextContent.Contains("Email is required."));
            Assert.Contains(messages, m => m.TextContent.Contains("Passwords do not match."));
            Assert.DoesNotContain("/login", navManager.Uri);
        });
    }

    [Fact]
    public async Task SubmitForm_WithValidInputs_CreatesCustomerAndNavigatesToLogin()
    {
        // 1. Arrange
        using var dbFactory = await SetupPageDependenciesAsync();
        var cut = Render<RegisterUserPage>();
        var navManager = Services.GetRequiredService<NavigationManager>();

        // 2. Act: fill in all 4 inputs (FullName, Email, Password, ConfirmPassword)
        var inputs = cut.FindAll("input.form-control");
        cut.FindAll("input.form-control")[0].Change("Alex Tan");
        cut.FindAll("input.form-control")[1].Change("alex.tan@example.com");
        cut.FindAll("input.form-control")[2].Change("Test@1234");
        cut.FindAll("input.form-control")[3].Change("Test@1234");

        cut.Find("form").Submit();

        // 3. Assert: verify user saved in SQLite and redirected to /login?registered=true
        await using var verifyDb = dbFactory.CreateDbContext();
        var savedUser = await verifyDb.Users.SingleAsync();
        Assert.Equal("100000000001", savedUser.Id);
        Assert.Equal("Alex Tan", savedUser.FullName);

        Assert.EndsWith("/login?registered=true", navManager.Uri);
    }

    [Fact]
    public async Task SubmitForm_WhenEmailAlreadyExists_DisplaysServerErrorBanner()
    {
        // 1. Arrange: pre-seed an existing user with the same email
        using var dbFactory = await SetupPageDependenciesAsync();
        var userManager = Services.GetRequiredService<UserManager<User>>();
        await userManager.CreateAsync(new User
        {
            Id = "100000000099",
            UserName = "alex.tan@example.com",
            Email = "alex.tan@example.com",
            FullName = "Existing Alex"
        }, "Test@1234");

        var cut = Render<RegisterUserPage>();

        // 2. Act: attempt to register again with alex.tan@example.com
        cut.FindAll("input.form-control")[0].Change("New Alex");
        cut.FindAll("input.form-control")[1].Change("alex.tan@example.com");
        cut.FindAll("input.form-control")[2].Change("Test@1234");
        cut.FindAll("input.form-control")[3].Change("Test@1234");

        cut.Find("form").Submit();

        // 3. Assert: error alert renders Identity duplicate error
        var alertDanger = cut.Find(".alert-danger");
        Assert.Contains("already taken", alertDanger.TextContent);
    }
}