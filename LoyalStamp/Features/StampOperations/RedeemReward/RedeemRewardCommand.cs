using LoyalStamp.Data;
using LoyalStamp.Data.Entities;
using LoyalStamp.Shared;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace LoyalStamp.Features.StampOperations.RedeemReward;

public record RedeemRewardResult(
    bool Succeeded,
    string Message,
    int RemainingStamps = 0,
    int TotalRedeemedCount = 0);

public class RedeemRewardCommand
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
    private readonly IHubContext<StampNotificationHub> _hubContext;

    public RedeemRewardCommand(
        IDbContextFactory<ApplicationDbContext> dbFactory,
        IHubContext<StampNotificationHub> hubContext)
    {
        _dbFactory = dbFactory;
        _hubContext = hubContext;
    }

    public async Task<RedeemRewardResult> RedeemAsync(
        Guid cardId,
        string? notes,
        string merchantUserId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var card = await db.CustomerCards
            .Include(cc => cc.Campaign)
                .ThenInclude(c => c.Merchant)
            .FirstOrDefaultAsync(cc => cc.Id == cardId);

        // Verify ownership
        if (card is null || card.Campaign.Merchant.UserId != merchantUserId)
        {
            return new RedeemRewardResult(false, "Unauthorized operation: card does not belong to your store.");
        }

        // Eligibility check: CurrentStamps must meet or exceed TargetStamps
        if (card.CurrentStamps < card.Campaign.TargetStamps)
        {
            return new RedeemRewardResult(
                false,
                $"Insufficient stamps to redeem. Customer has {card.CurrentStamps} / {card.Campaign.TargetStamps} stamps.");
        }

        int targetStamps = card.Campaign.TargetStamps;

        // Deduct TargetStamps while preserving any overflow carryover stamps
        card.CurrentStamps -= targetStamps;
        card.TotalRedeemedCount += 1;
        card.LastUpdated = DateTime.Now;

        var transaction = new StampTransaction
        {
            CustomerCardId = card.Id,
            TransactionType = "Redeem",
            StampsChanged = -targetStamps,
            SpendAmount = null,
            Notes = string.IsNullOrWhiteSpace(notes)
                ? $"Redeemed: {card.Campaign.RewardItem}"
                : notes.Trim(),
            CreatedAt = DateTime.Now
        };

        db.StampTransactions.Add(transaction);

        try
        {
            // RowVersion concurrency token prevents double-redemption from rapid double-clicks
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return new RedeemRewardResult(
                false,
                "Redemption conflict detected (possible duplicate submission). Please refresh the card status.");
        }

        // Push real-time UI update to the customer's active Blazor session via SignalR
        var notificationText = $"Reward redeemed at {card.Campaign.Merchant.StoreName}: {card.Campaign.RewardItem}! Remaining stamps: {card.CurrentStamps}.";
        await _hubContext.Clients
            .Group(StampNotificationHub.GetUserGroupName(card.UserId))
            .SendAsync("ReceiveStampUpdate", card.Id, notificationText);

        return new RedeemRewardResult(
            true,
            $"Reward \"{card.Campaign.RewardItem}\" redeemed! Carryover balance: {card.CurrentStamps} stamp(s).",
            card.CurrentStamps,
            card.TotalRedeemedCount);
    }
}