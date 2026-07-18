namespace NajaEcho.Application.Features.Loot.GetMemberLedger;

public sealed record LedgerEntryDto(
    Guid Id,
    string Kind,
    int Amount,
    string Reason,
    string PostedBy,
    DateTimeOffset CreatedAt);
