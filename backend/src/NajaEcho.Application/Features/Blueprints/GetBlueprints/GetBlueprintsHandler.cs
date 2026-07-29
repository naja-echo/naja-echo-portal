using NajaEcho.Application.Abstractions;

namespace NajaEcho.Application.Features.Blueprints.GetBlueprints;

public sealed class GetBlueprintsHandler(IBlueprintRepository repository)
{
    public async Task<IReadOnlyList<BlueprintListItemDto>> HandleAsync(
        GetBlueprintsQuery query, CancellationToken ct = default) =>
        await repository.GetListAsync(ct);
}
