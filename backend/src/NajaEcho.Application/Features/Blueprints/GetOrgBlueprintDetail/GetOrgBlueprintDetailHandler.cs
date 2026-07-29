using NajaEcho.Application.Abstractions;

namespace NajaEcho.Application.Features.Blueprints.GetOrgBlueprintDetail;

public sealed class GetOrgBlueprintDetailHandler(IOrgBlueprintRepository repository)
{
    public Task<OrgBlueprintDetailDto?> HandleAsync(
        GetOrgBlueprintDetailQuery query,
        CancellationToken ct = default) =>
        repository.GetDetailAsync(query.UserId, query.BlueprintId, ct);
}
