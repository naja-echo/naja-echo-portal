namespace NajaEcho.Api.Features.Loot.Contracts;

public sealed record LootDistributionResponse(IReadOnlyList<DistributionRowResponse> Members);
