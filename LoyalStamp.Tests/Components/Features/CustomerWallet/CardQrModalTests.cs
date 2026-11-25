using Bunit;
using LoyalStamp.Features.CustomerWallet.MyCards;

namespace LoyalStamp.Tests.Components.Features.CustomerWallet;

public class CardQrModalTests : BunitContext
{
    private static CustomerCardDto CreateSampleCard(int currentStamps = 3, int targetStamps = 8) =>
        new(
            CardId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
            CardCode: "LS-8A92B",
            StoreName: "Artisan Brew Coffee",
            LogoUrl: null,
            CampaignTitle: "Daily Espresso Club",
            MinSpendAmount: 5.00m,
            CurrentStamps: currentStamps,
            TargetStamps: targetStamps,
            RewardItem: "1 Free Latte",
            TotalRedeemedCount: 0,
            IsCampaignActive: true,
            LastUpdated: new DateTime(2026, 11, 20, 10, 0, 0)
        );

    [Fact]
    public void Render_WhenCardIsNull_RendersNothing()
    {
        // 1. Arrange & 2. Act
        var cut = Render<CardQrModal>(parameters => parameters
            .Add(p => p.Card, null));

        // 3. Assert
        Assert.Empty(cut.Markup.Trim());
    }

    [Fact]
    public void Render_WhenStampsBelowTarget_ShowsQrCodeFallbackCodeAndNeutralProgressAlert()
    {
        // 1. Arrange
        var card = CreateSampleCard(currentStamps: 3, targetStamps: 8);

        // 2. Act
        var cut = Render<CardQrModal>(parameters => parameters
            .Add(p => p.Card, card));

        // 3. Assert
        Assert.Contains("Artisan Brew Coffee", cut.Find(".modal-title").TextContent);
        Assert.Contains("LS-8A92B", cut.Find(".badge.bg-dark").TextContent);

        var qrImg = cut.Find("img[alt='Checkout QR Code']");
        Assert.StartsWith("data:image/png;base64,", qrImg.GetAttribute("src"));

        var progressAlert = cut.Find(".alert.alert-light");
        Assert.Contains("3 / 8 stamps", progressAlert.TextContent);
        Assert.DoesNotContain("Ready to redeem", cut.Markup);
    }

    [Fact]
    public void Render_WhenStampsMeetTarget_ShowsSuccessAlertWithRewardBanner()
    {
        // 1. Arrange
        var card = CreateSampleCard(currentStamps: 8, targetStamps: 8);

        // 2. Act
        var cut = Render<CardQrModal>(parameters => parameters
            .Add(p => p.Card, card));

        // 3. Assert
        var successAlert = cut.Find(".alert.alert-success");
        Assert.Contains("8 / 8 stamps", successAlert.TextContent);
        Assert.Contains("Ready to redeem: 1 Free Latte!", successAlert.TextContent);
    }

    [Fact]
    public async Task ClickEvents_ClickingInsideContentDoesNotClose_ClickingBackdropOrCloseInvokesOnClose()
    {
        // 1. Arrange
        var card = CreateSampleCard();
        int closeCount = 0;

        var cut = Render<CardQrModal>(parameters => parameters
            .Add(p => p.Card, card)
            .Add(p => p.OnClose, () => closeCount++));

        // 2. Act 1: verify .modal-dialog stops click propagation so backdrop OnClose will NOT fire
        var dialog = cut.Find(".modal-dialog");
        Assert.NotNull(dialog.GetAttribute("blazor:onclick:stoppropagation"));
        Assert.Equal(0, closeCount);

        // 3. Act 2: clicking the backdrop (.modal) -> OnClose fires
        await cut.Find(".modal").ClickAsync();
        Assert.Equal(1, closeCount);

        // 3. Act 3: clicking the header close button (or backdrop) -> OnClose fires
        await cut.Find(".btn-close").ClickAsync();
        Assert.Equal(2, closeCount);
    }
}