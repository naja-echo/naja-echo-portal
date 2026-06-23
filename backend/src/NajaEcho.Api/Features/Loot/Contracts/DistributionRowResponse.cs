namespace NajaEcho.Api.Features.Loot.Contracts;

public sealed record DistributionRowResponse(
    Guid MemberId,
    string DisplayName,
    int OrgPointsTotal,
    int LootPointsTotal,
    double ClaimPriority);
