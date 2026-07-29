using NajaEcho.Application.Features.Blueprints.GetBlueprintDetail;
using NajaEcho.Application.Features.Blueprints.GetMyBlueprints;

namespace NajaEcho.Application.Abstractions;

public interface IUserBlueprintRepository
{
    /// <summary>Returns all blueprints saved by the given user, ordered by product name.</summary>
    Task<IReadOnlyList<MyBlueprintListItemDto>> GetListAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Adds a blueprint to the user's personal list.
    /// Throws <see cref="DuplicateBlueprintException"/> if already present.
    /// Throws <see cref="BlueprintNotFoundException"/> if the catalog blueprint does not exist.
    /// </summary>
    Task<MyBlueprintListItemDto> AddAsync(Guid userId, Guid blueprintId, CancellationToken ct = default);

    /// <summary>
    /// Returns the full detail for a blueprint in the user's personal list, including ingredient slots.
    /// Returns null if the blueprint is not in the user's list.
    /// </summary>
    Task<BlueprintDetailDto?> GetDetailAsync(Guid userId, Guid blueprintId, CancellationToken ct = default);

    /// <summary>
    /// Removes a blueprint from the user's personal list.
    /// Returns true if removed, false if the blueprint was not in the user's list.
    /// </summary>
    Task<bool> RemoveAsync(Guid userId, Guid blueprintId, CancellationToken ct = default);
}
