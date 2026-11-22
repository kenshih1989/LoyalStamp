using Bunit;
using LoyalStamp.Components.Layout;

namespace LoyalStamp.Tests.Components.Layout;

public class NavMenuTests : BunitContext
{
    [Fact]
    public void Render_WhenNotAuthorized_ShowsGuestAuthLinksOnly()
    {
        // 1. Arrange: simulate an unauthenticated visitor
        this.AddAuthorization();

        // 2. Act: render NavMenu.razor in memory
        var cut = Render<NavMenu>();

        // 3. Assert: verify public Home link and 3 guest auth links exist
        Assert.NotEmpty(cut.FindAll("a.navbar-brand"));
        Assert.Equal("Login", cut.Find("a[href='/login']").TextContent.Trim());
        Assert.Equal("Customer Sign Up", cut.Find("a[href='/register-as-user']").TextContent.Trim());
        Assert.Equal("Merchant Sign Up", cut.Find("a[href='/register-as-merchant']").TextContent.Trim());

        // Ensure Merchant links, Customer links, and Logout form are hidden
        Assert.Empty(cut.FindAll("a[href='/merchant/dashboard']"));
        Assert.Empty(cut.FindAll("a[href='/merchant/campaigns']"));
        Assert.Empty(cut.FindAll("a[href='/merchant/issue-stamp']"));
        Assert.Empty(cut.FindAll("a[href='/explore']"));
        Assert.Empty(cut.FindAll("a[href='/my-wallet']"));
        Assert.Empty(cut.FindAll("a[href='/history']"));
        Assert.Empty(cut.FindAll("form[action='/logout']"));
    }

    [Fact]
    public void Render_WhenLoggedInAsMerchant_ShowsMerchantNavLinksAndLogoutFormOnly()
    {
        // 1. Arrange: simulate an authenticated Merchant
        var authContext = this.AddAuthorization();
        authContext.SetAuthorized("sarah@artisanbrew.com");
        authContext.SetRoles("Merchant");

        // 2. Act
        var cut = Render<NavMenu>();

        // 3. Assert: verify Merchant-specific navigation links
        Assert.Equal("Dashboard", cut.Find("a[href='/merchant/dashboard']").TextContent.Trim());
        Assert.Equal("My Campaigns", cut.Find("a[href='/merchant/campaigns']").TextContent.Trim());
        Assert.Equal("Issue / Redeem", cut.Find("a[href='/merchant/issue-stamp']").TextContent.Trim());

        // Verify Logout POST button is rendered
        var logoutButton = cut.Find("form[action='/logout'][method='post'] button[type='submit']");
        Assert.Equal("Logout", logoutButton.TextContent.Trim());

        // Ensure guest links and Customer-only links are strictly hidden
        Assert.Empty(cut.FindAll("a[href='/login']"));
        Assert.Empty(cut.FindAll("a[href='/register-as-user']"));
        Assert.Empty(cut.FindAll("a[href='/register-as-merchant']"));
        Assert.Empty(cut.FindAll("a[href='/explore']"));
        Assert.Empty(cut.FindAll("a[href='/my-wallet']"));
        Assert.Empty(cut.FindAll("a[href='/history']"));
    }

    [Fact]
    public void Render_WhenLoggedInAsCustomer_ShowsCustomerNavLinksAndLogoutFormOnly()
    {
        // 1. Arrange: simulate an authenticated Customer
        var authContext = this.AddAuthorization();
        authContext.SetAuthorized("alex.tan@example.com");
        authContext.SetRoles("Customer");

        // 2. Act
        var cut = Render<NavMenu>();

        // 3. Assert: verify Customer-specific navigation links
        Assert.Equal("Explore Campaigns", cut.Find("a[href='/explore']").TextContent.Trim());
        Assert.Equal("My Stamp Cards", cut.Find("a[href='/my-wallet']").TextContent.Trim());
        Assert.Equal("History", cut.Find("a[href='/history']").TextContent.Trim());

        // Verify Logout POST button is rendered
        var logoutButton = cut.Find("form[action='/logout'][method='post'] button[type='submit']");
        Assert.Equal("Logout", logoutButton.TextContent.Trim());

        // Ensure guest links and Merchant-only links are strictly hidden
        Assert.Empty(cut.FindAll("a[href='/login']"));
        Assert.Empty(cut.FindAll("a[href='/register-as-user']"));
        Assert.Empty(cut.FindAll("a[href='/register-as-merchant']"));
        Assert.Empty(cut.FindAll("a[href='/merchant/dashboard']"));
        Assert.Empty(cut.FindAll("a[href='/merchant/campaigns']"));
        Assert.Empty(cut.FindAll("a[href='/merchant/issue-stamp']"));
    }
}