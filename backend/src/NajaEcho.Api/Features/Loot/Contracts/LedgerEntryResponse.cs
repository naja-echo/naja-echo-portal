namespace NajaEcho.Api.Features.Loot.Contracts;

public sealed record LedgerEntryResponse(
    Guid Id,
    int Amount,
    string Reason,
    string PostedBy,
    DateTimeOffset CreatedAt);
