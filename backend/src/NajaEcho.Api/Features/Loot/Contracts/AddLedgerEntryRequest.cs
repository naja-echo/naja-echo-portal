using System.ComponentModel.DataAnnotations;

namespace NajaEcho.Api.Features.Loot.Contracts;

public sealed record AddLedgerEntryRequest(
    int Amount,
    [Required][MaxLength(500)] string Reason);
