using Bunit;
using LoyalStamp.Components.Pages;

namespace LoyalStamp.Tests.Features.Pages;

public class HomePageTests : BunitContext
{
    [Fact]
    public void HomePage_WhenNotAuthorized_ShowsRegisterAndLoginButtons()
    {
        // 1. Arrange (simulate an unauthorized user state)
        this.AddAuthorization();

        // 2. Act (render the Home.razor component in memory)
        var cut = Render<Home>();

        // 3. Assert (verify the page contains three authentication entry buttons and does not contain Logout)
        var markup = cut.Markup;
        Assert.Contains("Register as Merchant", markup);
        Assert.Contains("Register as Customer", markup);
        Assert.Contains("Login", markup);
        Assert.DoesNotContain("Logout", markup);
    }

    [Fact]
    public void HomePage_WhenLoggedInAsMerchant_ShowsDashboardAndLogoutButtons()
    {
        // 1. Arrange (simulate a logged-in merchant user state)
        var authContext = this.AddAuthorization();
        authContext.SetAuthorized("Sarah Lim");
        authContext.SetRoles("Merchant");

        // 2. Act (render the Home.razor component in memory)
        var cut = Render<Home>();

        // 3. Assert (verify the page shows Merchant Dashboard and Logout buttons, and does not show Register as Merchant)
        var markup = cut.Markup;
        Assert.Contains("Go to Merchant Dashboard", markup);
        Assert.Contains("Logout", markup);
        Assert.DoesNotContain("Register as Merchant", markup);
    }
}