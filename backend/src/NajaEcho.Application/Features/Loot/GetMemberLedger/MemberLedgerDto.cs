namespace NajaEcho.Application.Features.Loot.GetMemberLedger;

public sealed record MemberLedgerDto(
    Guid MemberId,
    string DisplayName,
    IReadOnlyList<LedgerEntryDetailDto> OrgPoints,
    IReadOnlyList<LedgerEntryDetailDto> LootPoints,
    int OrgPointsTotal,
    int LootPointsTotal,
    double ClaimPriority);

public sealed record LedgerEntryDetailDto(
    Guid Id,
    int Amount,
    string Reason,
    string PostedBy,
    DateTimeOffset CreatedAt);
