using NajaEcho.Application.Abstractions;

namespace NajaEcho.Application.Features.Loot.GetDistribution;

public sealed class GetDistributionHandler(ILootLedgerRepository repository)
{
    public async Task<IReadOnlyList<DistributionRowDto>> HandleAsync(GetDistributionQuery query, CancellationToken ct) =>
        await repository.GetDistributionAsync(ct);
}
