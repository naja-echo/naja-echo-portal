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
}
