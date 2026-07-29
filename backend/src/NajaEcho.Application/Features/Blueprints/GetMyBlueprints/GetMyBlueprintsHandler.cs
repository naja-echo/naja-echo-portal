using NajaEcho.Application.Abstractions;

namespace NajaEcho.Application.Features.Blueprints.GetMyBlueprints;

public sealed class GetMyBlueprintsHandler(IUserBlueprintRepository repository)
{
    public Task<IReadOnlyList<MyBlueprintListItemDto>> HandleAsync(GetMyBlueprintsQuery query, CancellationToken ct = default) =>
        repository.GetListAsync(query.UserId, ct);
}
