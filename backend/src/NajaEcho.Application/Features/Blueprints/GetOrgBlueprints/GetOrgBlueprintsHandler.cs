using NajaEcho.Application.Abstractions;

namespace NajaEcho.Application.Features.Blueprints.GetOrgBlueprints;

public sealed class GetOrgBlueprintsHandler(IOrgBlueprintRepository repository)
{
    public Task<IReadOnlyList<OrgBlueprintListItemDto>> HandleAsync(
        GetOrgBlueprintsQuery query,
        CancellationToken ct = default) =>
        repository.GetListAsync(query.UserId, ct);
}
