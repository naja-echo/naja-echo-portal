using Microsoft.EntityFrameworkCore;
using NajaEcho.Application.Abstractions;
using NajaEcho.Application.Features.Loot.GetDistribution;
using NajaEcho.Application.Features.Loot.GetMemberLedger;
using NajaEcho.Domain.Loot;
using NajaEcho.Infrastructure.Persistence;

namespace NajaEcho.Infrastructure.Loot;

public sealed class LootLedgerRepository(AppDbContext db) : ILootLedgerRepository
{
    public async Task AddEntryAsync(LootLedgerEntry entry, CancellationToken ct)
    {
        db.LootLedger.Add(entry);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<DistributionRowDto>> GetDistributionAsync(CancellationToken ct)
    {
        var rows = await db.LootMemberStandings
            .OrderBy(x => x.ClaimPriority)
            .ToListAsync(ct);

        return rows.Select(r => new DistributionRowDto(
            r.MemberId,
            r.DisplayName,
            r.OrgPointsTotal,
            r.LootPointsTotal,
            r.ClaimPriority))
            .ToList();
    }

    public async Task<MemberLedgerData?> GetMemberLedgerAsync(Guid memberId, CancellationToken ct)
    {
        var member = await db.Users
            .Where(u => u.Id == memberId)
            .Select(u => new { u.Id, u.DisplayName })
            .FirstOrDefaultAsync(ct);

        if (member is null) return null;

        var entries = await db.Database.SqlQuery<LedgerEntryRow>($"""
            SELECT
                l.id,
                l.kind,
                l.amount,
                l.reason,
                l.created_at,
                COALESCE(c.name, actor.display_name) AS posted_by
            FROM loot_ledger l
            JOIN "AspNetUsers" actor ON actor.id = l.actor_id
            LEFT JOIN LATERAL (
                SELECT name FROM characters
                WHERE owner_user_id = l.actor_id
                ORDER BY created_at
                LIMIT 1
            ) c ON TRUE
            WHERE l.member_id = {memberId}
            ORDER BY l.created_at DESC
            """).ToListAsync(ct);

        var dtos = entries.Select(e => new LedgerEntryDto(
            e.Id,
            e.Kind,
            e.Amount,
            e.Reason,
            e.PostedBy,
            e.CreatedAt))
            .ToList();

        return new MemberLedgerData(member.Id, member.DisplayName, dtos);
    }

    private sealed record LedgerEntryRow(
        Guid Id,
        string Kind,
        int Amount,
        string Reason,
        string PostedBy,
        DateTimeOffset CreatedAt);
}
