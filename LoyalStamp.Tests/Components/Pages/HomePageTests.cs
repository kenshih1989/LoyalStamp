using Bunit;
using LoyalStamp.Components.Pages;
using System.Security.Claims;

namespace LoyalStamp.Tests.Components.Pages;

public class HomePageTests : BunitContext
{
    [Fact]
    public void Render_WhenNotAuthorized_ShowsRegisterAndLoginLinksOnly()
    {
        // 1. Arrange: simulate an unauthenticated visitor
        this.AddAuthorization();

        // 2. Act: render Home.razor in memory
        var cut = Render<Home>();

        // 3. Assert: verify the 3 auth entry links exist with correct routes, and Logout form is hidden
        var merchantRegLink = cut.Find("a[href='/register-as-merchant']");
        var customerRegLink = cut.Find("a[href='/register-as-user']");
        var loginLink = cut.Find("a[href='/login']");

        Assert.Equal("Register as Merchant", merchantRegLink.TextContent.Trim());
        Assert.Equal("Register as Customer", customerRegLink.TextContent.Trim());
        Assert.Equal("Login", loginLink.TextContent.Trim());
        Assert.Empty(cut.FindAll("form[action='/logout']"));
    }

    [Fact]
    public void Render_WhenLoggedInAsMerchant_ShowsDashboardLinkFullNameAndLogoutForm()
    {
        // 1. Arrange: simulate an authenticated Merchant with FullName claim
        var authContext = this.AddAuthorization();
        authContext.SetAuthorized("sarah@artisanbrew.com");
        authContext.SetRoles("Merchant");
        authContext.SetClaims(new Claim("FullName", "Sarah Lim"));

        // 2. Act
        var cut = Render<Home>();

        // 3. Assert: verify personalized FullName greeting, Merchant Dashboard link, and Logout POST button
        var welcomeBanner = cut.Find(".alert");
        Assert.Contains("Welcome back,", welcomeBanner.TextContent);
        Assert.Contains("Sarah Lim", welcomeBanner.TextContent);

        var dashboardLink = cut.Find("a[href='/merchant/dashboard']");
        Assert.Equal("Go to Merchant Dashboard", dashboardLink.TextContent.Trim());

        var logoutButton = cut.Find("form[action='/logout'][method='post'] button[type='submit']");
        Assert.Equal("Logout", logoutButton.TextContent.Trim());

        // Ensure guest links and customer-only links are not rendered
        Assert.Empty(cut.FindAll("a[href='/register-as-merchant']"));
        Assert.Empty(cut.FindAll("a[href='/explore']"));
    }

    [Fact]
    public void Render_WhenLoggedInAsCustomer_ShowsExploreLinkFullNameAndLogoutForm()
    {
        // 1. Arrange: simulate an authenticated Customer with FullName claim
        var authContext = this.AddAuthorization();
        authContext.SetAuthorized("alex.tan@example.com");
        authContext.SetRoles("Customer");
        authContext.SetClaims(new Claim("FullName", "Alex Tan"));

        // 2. Act
        var cut = Render<Home>();

        // 3. Assert: verify personalized FullName greeting, Explore link, and Logout POST button
        var welcomeBanner = cut.Find(".alert");
        Assert.Contains("Alex Tan", welcomeBanner.TextContent);

        var exploreLink = cut.Find("a[href='/explore']");
        Assert.Equal("Explore Stamp Cards", exploreLink.TextContent.Trim());

        Assert.NotEmpty(cut.FindAll("form[action='/logout'][method='post'] button[type='submit']"));
        Assert.Empty(cut.FindAll("a[href='/merchant/dashboard']"));
    }
}