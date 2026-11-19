using LoyalStamp.Data.Entities;
using LoyalStamp.Features.StampOperations.IssueStamp;
using LoyalStamp.Shared;
using LoyalStamp.Tests.TestHelpers;
using Microsoft.AspNetCore.SignalR;
using Moq;

namespace LoyalStamp.Tests.Features.StampOperations;

public class IssueStampCommandTests
{
    [Fact]
    public async Task IssueAsync_ValidSpendWithOverflow_IssuesFlooredStampsAndRecordsTransaction()
    {
        // 1. Arrange (Prepare an isolated in-memory database and test data)
        using var dbFactory = new TestDbContextFactory();
        await using (var db = dbFactory.CreateDbContext())
        {
            var merchantUser = new User { Id = "200000000001", UserName = "sarah@test.com", Email = "sarah@test.com", FullName = "Sarah" };
            var customerUser = new User { Id = "100000000001", UserName = "alex@test.com", Email = "alex@test.com", FullName = "Alex" };
            db.Users.AddRange(merchantUser, customerUser);

            var merchant = new Merchant { Id = 1, UserId = merchantUser.Id, StoreName = "Artisan Brew" };
            db.Merchants.Add(merchant);

            var campaign = new StampCampaign
            {
                Id = 1,
                MerchantId = 1,
                Title = "Espresso Club",
                MinSpendAmount = 5.00m,
                TargetStamps = 8,
                RewardItem = "Free Latte",
                IsActive = true
            };
            db.StampCampaigns.Add(campaign);

            var cardId = Guid.NewGuid();
            db.CustomerCards.Add(new CustomerCard
            {
                Id = cardId,
                UserId = customerUser.Id,
                CampaignId = 1,
                CardCode = "LS-TEST1",
                CurrentStamps = 0,
                RowVersion = [1] // Provide initial concurrency token for SQLite in-memory testing
            });

            await db.SaveChangesAsync();

            // Mock SignalR Hub (to avoid real network push)
            var mockHub = new Mock<IHubContext<StampNotificationHub>>();
            var mockClients = new Mock<IHubClients>();
            var mockClientProxy = new Mock<IClientProxy>();
            mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(mockClientProxy.Object);
            mockHub.Setup(h => h.Clients).Returns(mockClients.Object);

            var command = new IssueStampCommand(dbFactory, mockHub.Object);

            // 2. Act (simulate a spend of $48.50, with a minimum spend of $5.00, expecting Floor(48.50/5) = 9 stamps)
            var result = await command.IssueAsync(cardId, 48.50m, "Receipt #101", merchantUser.Id);

            // 3. Assert (verify the result)
            Assert.True(result.Succeeded);
            Assert.Equal(9, result.StampsEarned);   // verify 9 stamps were earned in this transaction
            Assert.Equal(9, result.UpdatedStamps);  // allow exceeding the target of 8 stamps (overflow accumulation)

            // Verify SignalR targeted the exact customer group and sent "ReceiveStampUpdate" once
            mockClients.Verify(
                c => c.Group($"customer-{customerUser.Id}"),
                Times.Once);

            mockClientProxy.Verify(
                p => p.SendCoreAsync(
                    "ReceiveStampUpdate",
                    It.Is<object?[]>(args =>
                        args.Length == 2 &&
                        (Guid)args[0]! == cardId &&
                        args[1]!.ToString()!.Contains("+9 stamp(s)")),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }
}