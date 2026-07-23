using NajaEcho.Application.Features.Blueprints.GetOrgBlueprintDetail;
using NajaEcho.Application.Features.Blueprints.GetOrgBlueprints;

namespace NajaEcho.Application.Abstractions;

public interface IOrgBlueprintRepository
{
    /// <summary>
    /// Returns a deduplicated list of all blueprints held by any member of the current user's
    /// organization, ordered by product name.
    /// </summary>
    Task<IReadOnlyList<OrgBlueprintListItemDto>> GetListAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Returns full detail for a blueprint, including ingredient slots and the list of org members
    /// who have it in their personal collection.
    /// Returns null if no org member has this blueprint.
    /// </summary>
    Task<OrgBlueprintDetailDto?> GetDetailAsync(Guid userId, Guid blueprintId, CancellationToken ct = default);
}
