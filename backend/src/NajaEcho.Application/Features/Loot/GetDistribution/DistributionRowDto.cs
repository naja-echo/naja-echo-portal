namespace NajaEcho.Application.Features.Loot.GetDistribution;

public sealed record DistributionRowDto(
    Guid MemberId,
    string DisplayName,
    int OrgPointsTotal,
    int LootPointsTotal,
    double ClaimPriority);
