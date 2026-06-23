namespace NajaEcho.Api.Features.Loot.Contracts;

public sealed record MemberLedgerResponse(
    Guid MemberId,
    string DisplayName,
    IReadOnlyList<LedgerEntryResponse> OrgPoints,
    IReadOnlyList<LedgerEntryResponse> LootPoints,
    int OrgPointsTotal,
    int LootPointsTotal,
    double ClaimPriority);
