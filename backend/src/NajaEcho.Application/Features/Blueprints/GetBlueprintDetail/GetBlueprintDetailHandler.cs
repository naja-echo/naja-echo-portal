using NajaEcho.Application.Abstractions;

namespace NajaEcho.Application.Features.Blueprints.GetBlueprintDetail;

public sealed class GetBlueprintDetailHandler(IUserBlueprintRepository repository)
{
    public Task<BlueprintDetailDto?> HandleAsync(GetBlueprintDetailQuery query, CancellationToken ct = default) =>
        repository.GetDetailAsync(query.UserId, query.BlueprintId, ct);
}
