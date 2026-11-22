using Bunit;
using LoyalStamp.Data.Entities;
using LoyalStamp.Features.Auth.Login;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Security.Claims;

namespace LoyalStamp.Tests.Features.Auth;

public class LoginPageTests : BunitContext
{
    private (Mock<UserManager<User>> MockUserManager, Mock<SignInManager<User>> MockSignInManager) SetupIdentityMocks()
    {
        var userStore = new Mock<IUserStore<User>>();
        var mockUserManager = new Mock<UserManager<User>>(
            userStore.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        var contextAccessor = new Mock<IHttpContextAccessor>();
        var claimsFactory = new Mock<IUserClaimsPrincipalFactory<User>>();
        var mockSignInManager = new Mock<SignInManager<User>>(
            mockUserManager.Object,
            contextAccessor.Object,
            claimsFactory.Object,
            new Mock<IOptions<IdentityOptions>>().Object,
            new Mock<ILogger<SignInManager<User>>>().Object,
            new Mock<IAuthenticationSchemeProvider>().Object,
            new Mock<IUserConfirmation<User>>().Object);

        Services.AddSingleton(mockUserManager.Object);
        Services.AddSingleton(mockSignInManager.Object);

        return (mockUserManager, mockSignInManager);
    }

    [Fact]
    public void Render_WhenRegisteredQueryParamIsTrue_ShowsRegistrationSuccessBanner()
    {
        // 1. Arrange: navigate to /login?registered=true before rendering
        SetupIdentityMocks();
        var navManager = Services.GetRequiredService<NavigationManager>();
        navManager.NavigateTo("/login?registered=true");

        // 2. Act
        var cut = Render<LoginPage>();

        // 3. Assert
        var successAlert = cut.Find(".alert-success");
        Assert.Contains("Account created successfully!", successAlert.TextContent);
    }

    [Fact]
    public void SubmitForm_WithInvalidCredentials_DisplaysErrorAlert()
    {
        // 1. Arrange
        var (mockUserManager, mockSignInManager) = SetupIdentityMocks();
        var user = new User { Id = "100000000001", Email = "alex@test.com", FullName = "Alex Tan" };

        mockUserManager.Setup(m => m.FindByEmailAsync("alex@test.com")).ReturnsAsync(user);
        mockSignInManager
            .Setup(s => s.CheckPasswordSignInAsync(user, "WrongPass", false))
            .ReturnsAsync(SignInResult.Failed);

        var cut = Render<LoginPage>();

        // 2. Act
        cut.Find("input[type='email']").Change("alex@test.com");
        cut.Find("input[type='password']").Change("WrongPass");
        cut.Find("form").Submit();

        // 3. Assert
        var dangerAlert = cut.Find(".alert-danger");
        Assert.Contains("Invalid email or password", dangerAlert.TextContent);
    }

    [Fact]
    public void SubmitForm_WhenCustomerLogsIn_AttachesFullNameClaimAndNavigatesToExplore()
    {
        // 1. Arrange
        var (mockUserManager, mockSignInManager) = SetupIdentityMocks();
        var customer = new User { Id = "100000000001", Email = "alex@test.com", FullName = "Alex Tan" };

        mockUserManager.Setup(m => m.FindByEmailAsync("alex@test.com")).ReturnsAsync(customer);
        mockUserManager.Setup(m => m.IsInRoleAsync(customer, "Merchant")).ReturnsAsync(false);
        mockSignInManager
            .Setup(s => s.CheckPasswordSignInAsync(customer, "Test@1234", false))
            .ReturnsAsync(SignInResult.Success);

        var cut = Render<LoginPage>();
        var navManager = Services.GetRequiredService<NavigationManager>();

        // 2. Act
        cut.Find("input[type='email']").Change("alex@test.com");
        cut.Find("input[type='password']").Change("Test@1234");
        cut.Find("form").Submit();

        // 3. Assert: verify FullName claim was passed and redirected to /explore
        mockSignInManager.Verify(s => s.SignInWithClaimsAsync(
            customer,
            false,
            It.Is<IEnumerable<Claim>>(claims => claims.Any(c => c.Type == "FullName" && c.Value == "Alex Tan"))),
            Times.Once);

        Assert.EndsWith("/explore", navManager.Uri);
    }

    [Fact]
    public void SubmitForm_WhenMerchantLogsIn_NavigatesToMerchantDashboard()
    {
        // 1. Arrange
        var (mockUserManager, mockSignInManager) = SetupIdentityMocks();
        var merchant = new User { Id = "200000000001", Email = "sarah@test.com", FullName = "Sarah Lim" };

        mockUserManager.Setup(m => m.FindByEmailAsync("sarah@test.com")).ReturnsAsync(merchant);
        mockUserManager.Setup(m => m.IsInRoleAsync(merchant, "Merchant")).ReturnsAsync(true);
        mockSignInManager
            .Setup(s => s.CheckPasswordSignInAsync(merchant, "Test@1234", false))
            .ReturnsAsync(SignInResult.Success);

        var cut = Render<LoginPage>();
        var navManager = Services.GetRequiredService<NavigationManager>();

        // 2. Act
        cut.Find("input[type='email']").Change("sarah@test.com");
        cut.Find("input[type='password']").Change("Test@1234");
        cut.Find("form").Submit();

        // 3. Assert
        Assert.EndsWith("/merchant/dashboard", navManager.Uri);
    }
}