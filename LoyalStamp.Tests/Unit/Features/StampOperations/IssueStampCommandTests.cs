using LoyalStamp.Data;
using LoyalStamp.Data.Entities;
using LoyalStamp.Features.StampOperations.IssueStamp;
using LoyalStamp.Shared;
using LoyalStamp.Tests.TestHelpers;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace LoyalStamp.Tests.Unit.Features.StampOperations;

public class IssueStampCommandTests
{
    private const string OwnerMerchantUserId = "200000000001";
    private const string OtherMerchantUserId = "200000000002";
    private const string CustomerUserId = "100000000001";

    // Helper: Creates a fresh in-memory database seeded with 2 merchants, 1 customer, 1 active campaign, and 1 card
    private static async Task<(TestDbContextFactory DbFactory, Mock<IHubClients> MockClients, Mock<IClientProxy> MockClientProxy, Guid CardId)>
        SetupScenarioAsync(bool isCampaignActive = true, int initialStamps = 0)
    {
        var dbFactory = new TestDbContextFactory();
        var cardId = Guid.NewGuid();

        await using (var db = dbFactory.CreateDbContext())
        {
            var ownerMerchantUser = new User { Id = OwnerMerchantUserId, UserName = "sarah@test.com", Email = "sarah@test.com", FullName = "Sarah" };
            var otherMerchantUser = new User { Id = OtherMerchantUserId, UserName = "jason@test.com", Email = "jason@test.com", FullName = "Jason" };
            var customerUser = new User { Id = CustomerUserId, UserName = "alex@test.com", Email = "alex@test.com", FullName = "Alex" };
            db.Users.AddRange(ownerMerchantUser, otherMerchantUser, customerUser);

            db.Merchants.AddRange(
                new Merchant { Id = 1, UserId = OwnerMerchantUserId, StoreName = "Artisan Brew" },
                new Merchant { Id = 2, UserId = OtherMerchantUserId, StoreName = "Boba Avenue" }
            );

            db.StampCampaigns.Add(new StampCampaign
            {
                Id = 1,
                MerchantId = 1,
                Title = "Espresso Club",
                MinSpendAmount = 5.00m,
                TargetStamps = 8,
                RewardItem = "Free Latte",
                IsActive = isCampaignActive
            });

            db.CustomerCards.Add(new CustomerCard
            {
                Id = cardId,
                UserId = CustomerUserId,
                CampaignId = 1,
                CardCode = "LS-TEST1",
                CurrentStamps = initialStamps,
                RowVersion = [1]
            });

            await db.SaveChangesAsync();
        }

        var mockHub = new Mock<IHubContext<StampNotificationHub>>();
        var mockClients = new Mock<IHubClients>();
        var mockClientProxy = new Mock<IClientProxy>();
        mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(mockClientProxy.Object);
        mockHub.Setup(h => h.Clients).Returns(mockClients.Object);

        return (dbFactory, mockClients, mockClientProxy, cardId);
    }

    private static IssueStampCommand CreateCommand(IDbContextFactory<ApplicationDbContext> dbFactory, Mock<IHubClients> mockClients)
    {
        var mockHub = new Mock<IHubContext<StampNotificationHub>>();
        mockHub.Setup(h => h.Clients).Returns(mockClients.Object);
        return new IssueStampCommand(dbFactory, mockHub.Object);
    }

    // =========================================================================
    // IssueAsync Tests (Positive & Negative Cases, SignalR Push, Concurrency Handling)
    // =========================================================================

    // 1. Positive Case: Valid spend with floor math & overflow accumulation
    [Fact]
    public async Task IssueAsync_ValidSpendWithOverflow_IssuesFlooredStampsAndSendsSignalR()
    {
        var (dbFactory, mockClients, mockClientProxy, cardId) = await SetupScenarioAsync();
        using var _ = dbFactory;
        var command = CreateCommand(dbFactory, mockClients);

        var result = await command.IssueAsync(cardId, 48.50m, "Receipt #101", OwnerMerchantUserId);

        Assert.True(result.Succeeded);
        Assert.Equal(9, result.StampsEarned);
        Assert.Equal(9, result.UpdatedStamps);

        mockClientProxy.Verify(
            p => p.SendCoreAsync(
                "ReceiveStampUpdate",
                It.Is<object?[]>(args => args.Length == 2 && (Guid)args[0]! == cardId && args[1]!.ToString()!.Contains("+9 stamp(s)")),
                It.IsAny<CancellationToken>()), //CancellationToken.None
            Times.Once);
    }

    // 2. Negative Case: Spend amount <= 0 (0 and negative values)
    [Theory]
    [InlineData(0)]
    [InlineData(-15.50)]
    public async Task IssueAsync_ZeroOrNegativeSpend_ReturnsFailureAndDoesNotPushSignalR(decimal invalidSpend)
    {
        var (dbFactory, mockClients, mockClientProxy, cardId) = await SetupScenarioAsync();
        using var _ = dbFactory;
        var command = CreateCommand(dbFactory, mockClients);

        var result = await command.IssueAsync(cardId, invalidSpend, null, OwnerMerchantUserId);

        Assert.False(result.Succeeded);
        Assert.Contains("greater than $0.00", result.Message);
        mockClientProxy.Verify(
            p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // 3. Negative Case: Empty Guid or non-existent cardId
    [Fact]
    public async Task IssueAsync_EmptyOrUnknownCardId_ReturnsUnauthorizedFailure()
    {
        var (dbFactory, mockClients, _, _) = await SetupScenarioAsync();
        using var _ = dbFactory;
        var command = CreateCommand(dbFactory, mockClients);

        var emptyResult = await command.IssueAsync(Guid.Empty, 20.00m, null, OwnerMerchantUserId);
        var unknownResult = await command.IssueAsync(Guid.NewGuid(), 20.00m, null, OwnerMerchantUserId);

        Assert.False(emptyResult.Succeeded);
        Assert.Contains("Unauthorized", emptyResult.Message);
        Assert.False(unknownResult.Succeeded);
        Assert.Contains("Unauthorized", unknownResult.Message);
    }

    // 4. Negative Case: Cross-merchant ownership violation
    [Fact]
    public async Task IssueAsync_CardBelongsToAnotherMerchant_BlocksOperationAndLeavesBalanceUnchanged()
    {
        var (dbFactory, mockClients, mockClientProxy, cardId) = await SetupScenarioAsync(initialStamps: 2);
        using var _ = dbFactory;
        var command = CreateCommand(dbFactory, mockClients);

        // Jason (Merchant 2) tries to issue stamps on Sarah's (Merchant 1) customer card
        var result = await command.IssueAsync(cardId, 25.00m, null, OtherMerchantUserId);

        Assert.False(result.Succeeded);
        Assert.Contains("Unauthorized", result.Message);

        // Verify database balance was untouched
        await using var verifyDb = dbFactory.CreateDbContext();
        var card = await verifyDb.CustomerCards.FindAsync(cardId);
        Assert.Equal(2, card!.CurrentStamps);

        mockClientProxy.Verify(
            p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // 5. Negative Case: Inactive campaign
    [Fact]
    public async Task IssueAsync_InactiveCampaign_ReturnsFailure()
    {
        var (dbFactory, mockClients, _, cardId) = await SetupScenarioAsync(isCampaignActive: false);
        using var _ = dbFactory;
        var command = CreateCommand(dbFactory, mockClients);

        var result = await command.IssueAsync(cardId, 25.00m, null, OwnerMerchantUserId);

        Assert.False(result.Succeeded);
        Assert.Contains("currently inactive", result.Message);
    }

    // 6. Negative Case: Spend amount below MinSpendAmount ($4.99 < $5.00 -> Floor = 0 stamps)
    [Fact]
    public async Task IssueAsync_SpendBelowMinimumThreshold_ReturnsFailureAndRecordsNoTransaction()
    {
        var (dbFactory, mockClients, _, cardId) = await SetupScenarioAsync();
        using var _ = dbFactory;
        var command = CreateCommand(dbFactory, mockClients);

        var result = await command.IssueAsync(cardId, 4.99m, null, OwnerMerchantUserId);

        Assert.False(result.Succeeded);
        Assert.Contains("below the minimum spend", result.Message);

        await using var verifyDb = dbFactory.CreateDbContext();
        Assert.Empty(await verifyDb.StampTransactions.ToListAsync());
    }

    // 7. Negative Case: Duplicate / Concurrent submission (DbUpdateConcurrencyException)
    [Fact]
    public async Task IssueAsync_ConcurrentModification_CatchesConcurrencyExceptionGracefully()
    {
        var (dbFactory, mockClients, _, cardId) = await SetupScenarioAsync();
        using var _ = dbFactory;

        // Wrap dbFactory so that right before SaveChangesAsync executes, another request mutates RowVersion in DB
        var racingFactory = new ConcurrentRaceDbContextFactory(dbFactory, cardId);
        var command = CreateCommand(racingFactory, mockClients);

        var result = await command.IssueAsync(cardId, 10.00m, "Double-click test", OwnerMerchantUserId);

        Assert.False(result.Succeeded);
        Assert.Contains("changed concurrently", result.Message);
    }

    // Helper wrapper to simulate another request updating RowVersion mid-flight before SaveChangesAsync
    private sealed class ConcurrentRaceDbContextFactory(TestDbContextFactory innerFactory, Guid targetCardId)
        : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext()
        {
            var context = innerFactory.CreateDbContext();
            context.SavingChanges += (_, _) =>
            {
                // Simulate a competing request incrementing RowVersion in the database right before save
                using var competingDb = innerFactory.CreateDbContext();
                competingDb.Database.ExecuteSqlRaw(
                    "UPDATE CustomerCards SET RowVersion = X'02' WHERE Id = {0}", targetCardId);
            };
            return context;
        }
    }

    // =========================================================================
    // LookupCardAsync Tests (QR Guid, Fallback CardCode & Store Ownership Guard)
    // =========================================================================


    // 1. Positive Case: Valid Guid with whitespace padding
    [Fact]
    public async Task LookupCardAsync_WithValidGuid_ReturnsCustomerCardDetails()
    {
        var (dbFactory, mockClients, _, cardId) = await SetupScenarioAsync(initialStamps: 3);
        using var _ = dbFactory;
        var command = CreateCommand(dbFactory, mockClients);

        // Simulate scanning QR code (Guid payload with accidental whitespace)
        var (card, error) = await command.LookupCardAsync($"  {cardId}  ", OwnerMerchantUserId);

        Assert.Null(error);
        Assert.NotNull(card);
        Assert.Equal(cardId, card.CardId);
        Assert.Equal("Alex", card.CustomerName);
        Assert.Equal(3, card.CurrentStamps);
    }

    // 2. Positive Case: Valid short alphanumeric fallback code (case-insensitive)
    [Fact]
    public async Task LookupCardAsync_WithLowercaseFallbackCode_NormalizesAndReturnsCard()
    {
        var (dbFactory, mockClients, _, cardId) = await SetupScenarioAsync();
        using var _ = dbFactory;
        var command = CreateCommand(dbFactory, mockClients);

        // Simulate cashier typing lowercase short code "ls-test1"
        var (card, error) = await command.LookupCardAsync("ls-test1", OwnerMerchantUserId);

        Assert.Null(error);
        Assert.NotNull(card);
        Assert.Equal(cardId, card.CardId);
        Assert.Equal("LS-TEST1", card.CardCode);
    }

    // 3. Negative Case: Card belongs to another merchant (ownership violation)
    [Fact]
    public async Task LookupCardAsync_CardBelongsToAnotherMerchant_BlocksDataLeakage()
    {
        var (dbFactory, mockClients, _, cardId) = await SetupScenarioAsync();
        using var _ = dbFactory;
        var command = CreateCommand(dbFactory, mockClients);

        // Jason (Merchant 2) tries to look up Sarah's (Merchant 1) customer card
        var (card, error) = await command.LookupCardAsync(cardId.ToString(), OtherMerchantUserId);

        Assert.Null(card);
        Assert.Contains("does not belong to your store", error);
    }


    // 4. Negative Case: Empty input or unknown code
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("LS-WRONG")]
    public async Task LookupCardAsync_EmptyOrUnknownCode_ReturnsErrorMessage(string invalidInput)
    {
        var (dbFactory, mockClients, _, _) = await SetupScenarioAsync();
        using var _ = dbFactory;
        var command = CreateCommand(dbFactory, mockClients);

        var (card, error) = await command.LookupCardAsync(invalidInput, OwnerMerchantUserId);

        Assert.Null(card);
        Assert.NotNull(error);
    }
}