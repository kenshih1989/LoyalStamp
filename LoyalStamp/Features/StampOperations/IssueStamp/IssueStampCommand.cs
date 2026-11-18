using LoyalStamp.Data;
using LoyalStamp.Data.Entities;
using LoyalStamp.Shared;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace LoyalStamp.Features.StampOperations.IssueStamp;

public record ScannedCardDetailsDto(
    Guid CardId,
    string CardCode,
    string CustomerName,
    string CustomerEmail,
    string CampaignTitle,
    decimal MinSpendAmount,
    int CurrentStamps,
    int TargetStamps,
    string RewardItem,
    int TotalRedeemedCount,
    bool IsCampaignActive);

public record IssueStampResult(
    bool Succeeded,
    string Message,
    int StampsEarned = 0,
    int UpdatedStamps = 0);

public class IssueStampCommand
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
    private readonly IHubContext<StampNotificationHub> _hubContext;

    public IssueStampCommand(
        IDbContextFactory<ApplicationDbContext> dbFactory,
        IHubContext<StampNotificationHub> hubContext)
    {
        _dbFactory = dbFactory;
        _hubContext = hubContext;
    }

    public async Task<(ScannedCardDetailsDto? Card, string? ErrorMessage)> LookupCardAsync(
        string codeInput,
        string merchantUserId)
    {
        if (string.IsNullOrWhiteSpace(codeInput))
        {
            return (null, "Please scan a QR code or enter a fallback card code.");
        }

        var trimmed = codeInput.Trim();
        await using var db = await _dbFactory.CreateDbContextAsync();

        IQueryable<CustomerCard> query = db.CustomerCards
            .AsNoTracking()
            .Include(cc => cc.User)
            .Include(cc => cc.Campaign)
                .ThenInclude(c => c.Merchant);

        CustomerCard? card;

        // Support both QR Guid payload and short alphanumeric CardCode (e.g., LS-8A92B)
        if (Guid.TryParse(trimmed, out var parsedCardId))
        {
            card = await query.FirstOrDefaultAsync(cc => cc.Id == parsedCardId);
        }
        else
        {
            var normalizedCode = trimmed.ToUpperInvariant();
            card = await query.FirstOrDefaultAsync(cc => cc.CardCode == normalizedCode);
        }

        // Ownership guard: ensure the card belongs to a campaign owned by the logged-in merchant
        if (card is null || card.Campaign.Merchant.UserId != merchantUserId)
        {
            return (null, "Invalid card code, or this stamp card does not belong to your store.");
        }

        var dto = new ScannedCardDetailsDto(
            card.Id,
            card.CardCode,
            card.User.FullName,
            card.User.Email ?? string.Empty,
            card.Campaign.Title,
            card.Campaign.MinSpendAmount,
            card.CurrentStamps,
            card.Campaign.TargetStamps,
            card.Campaign.RewardItem,
            card.TotalRedeemedCount,
            card.Campaign.IsActive);

        return (dto, null);
    }

    public async Task<IssueStampResult> IssueAsync(
        Guid cardId,
        decimal spendAmount,
        string? notes,
        string merchantUserId)
    {
        if (spendAmount <= 0)
        {
            return new IssueStampResult(false, "Spend amount must be greater than $0.00.");
        }

        await using var db = await _dbFactory.CreateDbContextAsync();

        var card = await db.CustomerCards
            .Include(cc => cc.Campaign)
                .ThenInclude(c => c.Merchant)
            .FirstOrDefaultAsync(cc => cc.Id == cardId);

        // Verify ownership again before mutating state
        if (card is null || card.Campaign.Merchant.UserId != merchantUserId)
        {
            return new IssueStampResult(false, "Unauthorized operation: card does not belong to your store.");
        }

        if (!card.Campaign.IsActive)
        {
            return new IssueStampResult(false, "This campaign is currently inactive and cannot issue new stamps.");
        }

        // Calculation rule: Floor(SpendAmount / MinSpendAmount)
        var roundedSpend = Math.Round(spendAmount, 2);
        int stampsEarned = (int)Math.Floor(roundedSpend / card.Campaign.MinSpendAmount);

        if (stampsEarned <= 0)
        {
            return new IssueStampResult(
                false,
                $"Spend amount (${roundedSpend:F2}) is below the minimum spend of ${card.Campaign.MinSpendAmount:F2} required for 1 stamp.");
        }

        // Allow stamps to overflow beyond TargetStamps for carryover
        card.CurrentStamps += stampsEarned;
        card.LastUpdated = DateTime.Now;

        var transaction = new StampTransaction
        {
            CustomerCardId = card.Id,
            TransactionType = "Earn",
            StampsChanged = stampsEarned,
            SpendAmount = roundedSpend,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            CreatedAt = DateTime.Now
        };

        db.StampTransactions.Add(transaction);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return new IssueStampResult(false, "Card state changed concurrently. Please re-scan the card and try again.");
        }

        // Push real-time UI update to the customer's active Blazor session via SignalR
        var notificationText = $"You earned +{stampsEarned} stamp(s) at {card.Campaign.Merchant.StoreName} ({card.CurrentStamps}/{card.Campaign.TargetStamps})!";
        await _hubContext.Clients
            .Group(StampNotificationHub.GetUserGroupName(card.UserId))
            .SendAsync("ReceiveStampUpdate", card.Id, notificationText);

        return new IssueStampResult(
            true,
            $"Issued {stampsEarned} stamp(s)! Customer now has {card.CurrentStamps} / {card.Campaign.TargetStamps} stamps.",
            stampsEarned,
            card.CurrentStamps);
    }
}