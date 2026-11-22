using LoyalStamp.Data;
using LoyalStamp.Data.Entities;
using LoyalStamp.Features.StampOperations.RedeemReward;
using LoyalStamp.Shared;
using LoyalStamp.Tests.TestHelpers;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace LoyalStamp.Tests.Unit.Features.StampOperations;

public class RedeemRewardCommandTests
{
    private const string OwnerMerchantUserId = "200000000001";
    private const string OtherMerchantUserId = "200000000002";
    private const string CustomerUserId = "100000000001";

    // Helper: Creates an isolated in-memory SQLite database with 2 merchants, 1 customer, 1 campaign (Target = 8), and 1 card
    private static async Task<(TestDbContextFactory DbFactory, Mock<IHubClients> MockClients, Mock<IClientProxy> MockClientProxy, Guid CardId)>
        SetupScenarioAsync(int initialStamps = 8, int initialRedeemedCount = 0)
    {
        var dbFactory = new TestDbContextFactory();
        var cardId = Guid.NewGuid();

        await using (var db = dbFactory.CreateDbContext())
        {
            db.Users.AddRange(
                new User { Id = OwnerMerchantUserId, UserName = "sarah@test.com", Email = "sarah@test.com", FullName = "Sarah" },
                new User { Id = OtherMerchantUserId, UserName = "jason@test.com", Email = "jason@test.com", FullName = "Jason" },
                new User { Id = CustomerUserId, UserName = "alex@test.com", Email = "alex@test.com", FullName = "Alex" }
            );

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
                RewardItem = "1 Free Handcrafted Latte",
                IsActive = true
            });

            db.CustomerCards.Add(new CustomerCard
            {
                Id = cardId,
                UserId = CustomerUserId,
                CampaignId = 1,
                CardCode = "LS-REDEEM",
                CurrentStamps = initialStamps,
                TotalRedeemedCount = initialRedeemedCount,
                RowVersion = [1]
            });

            await db.SaveChangesAsync();
        }

        var mockClients = new Mock<IHubClients>();
        var mockClientProxy = new Mock<IClientProxy>();
        mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(mockClientProxy.Object);

        return (dbFactory, mockClients, mockClientProxy, cardId);
    }

    private static RedeemRewardCommand CreateCommand(IDbContextFactory<ApplicationDbContext> dbFactory, Mock<IHubClients> mockClients)
    {
        var mockHub = new Mock<IHubContext<StampNotificationHub>>();
        mockHub.Setup(h => h.Clients).Returns(mockClients.Object);
        return new RedeemRewardCommand(dbFactory, mockHub.Object);
    }

    // 1. Positive Case: Exact target stamps met -> resets to 0, increments redeemed count, logs Redeem transaction, and pushes SignalR
    [Fact]
    public async Task RedeemAsync_ExactTargetStampsMet_ResetsBalanceAndLogsTransaction()
    {
        var (dbFactory, mockClients, mockClientProxy, cardId) = await SetupScenarioAsync(initialStamps: 8, initialRedeemedCount: 0);
        using var _ = dbFactory;
        var command = CreateCommand(dbFactory, mockClients);

        var result = await command.RedeemAsync(cardId, "Counter pickup", OwnerMerchantUserId);

        Assert.True(result.Succeeded);
        Assert.Equal(0, result.RemainingStamps);
        Assert.Equal(1, result.TotalRedeemedCount);

        await using var verifyDb = dbFactory.CreateDbContext();
        var transaction = await verifyDb.StampTransactions.SingleAsync();
        Assert.Equal("Redeem", transaction.TransactionType);
        Assert.Equal(-8, transaction.StampsChanged);
        Assert.Null(transaction.SpendAmount);
        Assert.Equal("Counter pickup", transaction.Notes);

        mockClients.Verify(c => c.Group($"customer-{CustomerUserId}"), Times.Once);
        mockClientProxy.Verify(
            p => p.SendCoreAsync(
                "ReceiveStampUpdate",
                It.Is<object?[]>(args =>
                    args.Length == 2 &&
                    (Guid)args[0]! == cardId &&
                    args[1]!.ToString()!.Contains("Reward redeemed at Artisan Brew")),
                CancellationToken.None),
            Times.Once);
    }

    // 2. Positive Case: Overflow carryover stamps preserved after redemption (11 - 8 = 3)
    [Fact]
    public async Task RedeemAsync_WithOverflowStamps_PreservesCarryoverBalanceAndUsesDefaultNoteWhenEmpty()
    {
        var (dbFactory, mockClients, _, cardId) = await SetupScenarioAsync(initialStamps: 11, initialRedeemedCount: 2);
        using var _ = dbFactory;
        var command = CreateCommand(dbFactory, mockClients);

        var result = await command.RedeemAsync(cardId, "   ", OwnerMerchantUserId);

        Assert.True(result.Succeeded);
        Assert.Equal(3, result.RemainingStamps);
        Assert.Equal(3, result.TotalRedeemedCount);

        await using var verifyDb = dbFactory.CreateDbContext();
        var transaction = await verifyDb.StampTransactions.SingleAsync();
        Assert.Equal("Redeemed: 1 Free Handcrafted Latte", transaction.Notes);
    }

    // 3. Negative Case: Insufficient stamps (CurrentStamps < TargetStamps)
    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    public async Task RedeemAsync_InsufficientStamps_ReturnsFailureAndDoesNotMutateDatabase(int currentStamps)
    {
        var (dbFactory, mockClients, mockClientProxy, cardId) = await SetupScenarioAsync(initialStamps: currentStamps);
        using var _ = dbFactory;
        var command = CreateCommand(dbFactory, mockClients);

        var result = await command.RedeemAsync(cardId, null, OwnerMerchantUserId);

        Assert.False(result.Succeeded);
        Assert.Contains("Insufficient stamps to redeem", result.Message);

        await using var verifyDb = dbFactory.CreateDbContext();
        Assert.Empty(await verifyDb.StampTransactions.ToListAsync());
        mockClientProxy.Verify(
            p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // 4. Negative Case: Empty Guid or unknown cardId
    [Fact]
    public async Task RedeemAsync_EmptyOrUnknownCardId_ReturnsUnauthorizedFailure()
    {
        var (dbFactory, mockClients, _, _) = await SetupScenarioAsync();
        using var _ = dbFactory;
        var command = CreateCommand(dbFactory, mockClients);

        var emptyResult = await command.RedeemAsync(Guid.Empty, null, OwnerMerchantUserId);
        var unknownResult = await command.RedeemAsync(Guid.NewGuid(), null, OwnerMerchantUserId);

        Assert.False(emptyResult.Succeeded);
        Assert.Contains("Unauthorized", emptyResult.Message);
        Assert.False(unknownResult.Succeeded);
        Assert.Contains("Unauthorized", unknownResult.Message);
    }

    // 5. Negative Case: Cross-merchant ownership violation
    [Fact]
    public async Task RedeemAsync_CardBelongsToAnotherMerchant_BlocksRedemptionAndPreservesStamps()
    {
        var (dbFactory, mockClients, mockClientProxy, cardId) = await SetupScenarioAsync(initialStamps: 8);
        using var _ = dbFactory;
        var command = CreateCommand(dbFactory, mockClients);

        // Jason (Merchant 2) tries to redeem Sarah's (Merchant 1) customer card
        var result = await command.RedeemAsync(cardId, null, OtherMerchantUserId);

        Assert.False(result.Succeeded);
        Assert.Contains("Unauthorized", result.Message);

        await using var verifyDb = dbFactory.CreateDbContext();
        var card = await verifyDb.CustomerCards.FindAsync(cardId);
        Assert.Equal(8, card!.CurrentStamps);
        Assert.Equal(0, card.TotalRedeemedCount);

        mockClientProxy.Verify(
            p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // 6. Negative Case: Concurrent double-click / double-redemption guard (DbUpdateConcurrencyException)
    [Fact]
    public async Task RedeemAsync_ConcurrentDoubleRedemption_CatchesConcurrencyExceptionGracefully()
    {
        var (dbFactory, mockClients, _, cardId) = await SetupScenarioAsync(initialStamps: 8);
        using var _ = dbFactory;

        var racingFactory = new ConcurrentRaceDbContextFactory(dbFactory, cardId);
        var command = CreateCommand(racingFactory, mockClients);

        var result = await command.RedeemAsync(cardId, "Double-click test", OwnerMerchantUserId);

        Assert.False(result.Succeeded);
        Assert.Contains("Redemption conflict detected", result.Message);
    }

    private sealed class ConcurrentRaceDbContextFactory(TestDbContextFactory innerFactory, Guid targetCardId)
        : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext()
        {
            var context = innerFactory.CreateDbContext();
            context.SavingChanges += (_, _) =>
            {
                using var competingDb = innerFactory.CreateDbContext();
                competingDb.Database.ExecuteSqlRaw(
                    "UPDATE CustomerCards SET RowVersion = X'02' WHERE Id = {0}", targetCardId);
            };
            return context;
        }
    }
}