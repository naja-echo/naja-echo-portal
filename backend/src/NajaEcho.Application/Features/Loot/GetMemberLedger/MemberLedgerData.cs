namespace NajaEcho.Application.Features.Loot.GetMemberLedger;

public sealed record MemberLedgerData(
    Guid MemberId,
    string DisplayName,
    IReadOnlyList<LedgerEntryDto> Entries);
