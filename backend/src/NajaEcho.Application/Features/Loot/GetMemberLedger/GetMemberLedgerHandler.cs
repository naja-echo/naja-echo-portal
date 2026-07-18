using NajaEcho.Application.Abstractions;

namespace NajaEcho.Application.Features.Loot.GetMemberLedger;

public sealed class GetMemberLedgerHandler(ILootLedgerRepository repository)
{
    public async Task<MemberLedgerDto> HandleAsync(GetMemberLedgerQuery query, CancellationToken ct)
    {
        var data = await repository.GetMemberLedgerAsync(query.MemberId, ct);
        if (data is null)
            throw new MemberNotFoundException(query.MemberId);

        var orgEntries = data.Entries
            .Where(e => e.Kind == "OrgPoints")
            .Select(MapEntry)
            .ToList();

        var lootEntries = data.Entries
            .Where(e => e.Kind == "LootPoints")
            .Select(MapEntry)
            .ToList();

        var orgTotal = orgEntries.Sum(e => e.Amount);
        var lootTotal = lootEntries.Sum(e => e.Amount);
        var priority = ClaimPriority.Compute(orgTotal, lootTotal);

        return new MemberLedgerDto(
            data.MemberId,
            data.DisplayName,
            orgEntries,
            lootEntries,
            orgTotal,
            lootTotal,
            priority);
    }

    private static LedgerEntryDetailDto MapEntry(LedgerEntryDto e) =>
        new(e.Id, e.Amount, e.Reason, e.PostedBy, e.CreatedAt);
}
