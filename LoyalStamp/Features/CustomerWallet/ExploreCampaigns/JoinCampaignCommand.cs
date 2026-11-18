using LoyalStamp.Data;
using LoyalStamp.Data.Entities;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace LoyalStamp.Features.CustomerWallet.ExploreCampaigns;

public record ExploreCampaignDto(
    int CampaignId,
    string StoreName,
    string? StoreDescription,
    string? LogoUrl,
    string Title,
    decimal MinSpendAmount,
    int TargetStamps,
    string RewardItem,
    bool IsAlreadyJoined);

public class JoinCampaignCommand
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;

    public JoinCampaignCommand(IDbContextFactory<ApplicationDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<List<ExploreCampaignDto>> GetActiveCampaignsAsync(string userId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var joinedCampaignIds = await db.CustomerCards
            .AsNoTracking()
            .Where(cc => cc.UserId == userId)
            .Select(cc => cc.CampaignId)
            .ToHashSetAsync();

        var activeCampaigns = await db.StampCampaigns
            .AsNoTracking()
            .Include(c => c.Merchant)
            .Where(c => c.IsActive)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

        return activeCampaigns.Select(c => new ExploreCampaignDto(
            c.Id,
            c.Merchant.StoreName,
            c.Merchant.Description,
            c.Merchant.LogoUrl,
            c.Title,
            c.MinSpendAmount,
            c.TargetStamps,
            c.RewardItem,
            joinedCampaignIds.Contains(c.Id)
        )).ToList();
    }

    public async Task<(bool Succeeded, string? ErrorMessage)> JoinAsync(int campaignId, string userId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var campaign = await db.StampCampaigns
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == campaignId && c.IsActive);

        if (campaign is null)
        {
            return (false, "This campaign is no longer available.");
        }

        // 应用层防重复检查
        bool alreadyExists = await db.CustomerCards
            .AnyAsync(cc => cc.UserId == userId && cc.CampaignId == campaignId);

        if (alreadyExists)
        {
            return (false, "You already have this stamp card in your wallet.");
        }

        // 生成唯一的短码 (例如: LS-8A92B)
        string cardCode;
        do
        {
            cardCode = GenerateShortCardCode();
        } while (await db.CustomerCards.AnyAsync(cc => cc.CardCode == cardCode));

        var newCard = new CustomerCard
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CampaignId = campaignId,
            CardCode = cardCode,
            CurrentStamps = 0,
            TotalRedeemedCount = 0,
            LastUpdated = DateTime.Now
        };

        try
        {
            db.CustomerCards.Add(newCard);
            await db.SaveChangesAsync();
            return (true, null);
        }
        catch (DbUpdateException)
        {
            // 数据库复合唯一索引兜底防并发重复提交
            return (false, "You have already joined this campaign.");
        }
    }

    private static string GenerateShortCardCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // 排除易混淆的 0/O/1/I
        Span<char> buffer = stackalloc char[5];
        for (int i = 0; i < buffer.Length; i++)
        {
            buffer[i] = chars[RandomNumberGenerator.GetInt32(chars.Length)];
        }
        return $"LS-{new string(buffer)}";
    }
}