using NajaEcho.Application.Abstractions;

namespace NajaEcho.Application.Features.Blueprints.SearchBlueprints;

public sealed class SearchBlueprintsHandler(IBlueprintRepository repository)
{
    public Task<IReadOnlyList<BlueprintSearchResultDto>> HandleAsync(SearchBlueprintsQuery query, CancellationToken ct = default) =>
        repository.SearchAsync(query.Term, limit: 20, ct);
}
