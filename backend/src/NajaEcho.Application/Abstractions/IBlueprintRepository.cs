using NajaEcho.Application.Features.Blueprints.GetBlueprints;
using NajaEcho.Application.Features.Blueprints.ImportBlueprints;
using NajaEcho.Domain.Blueprints;

namespace NajaEcho.Application.Abstractions;

/// <summary>An <c>sc.items</c> match for a crafting material name — the item's <c>uex_id</c> (FR-009).</summary>
public sealed record MaterialMatch(int UexId);

/// <summary>Persistence counts returned by a blueprint import.</summary>
public sealed record BlueprintImportCounts(int BlueprintsInserted, int BlueprintsUpdated);

public interface IBlueprintRepository
{
    /// <summary>
    /// Resolves distinct crafting-material names to <c>sc.items</c> <c>uex_id</c> values (excluding
    /// soft-deleted rows); case-insensitive; deterministic winner (lowest <c>uex_id</c>) on
    /// duplicate names. Returned keys are lower-cased material names (research Decision 11).
    /// </summary>
    Task<IReadOnlyDictionary<string, MaterialMatch>> ResolveMaterialsAsync(
        IReadOnlyCollection<string> names, CancellationToken ct = default);

    /// <summary>
    /// Persists the dataset in a single transaction (FR-021a): reference data replaced wholesale,
    /// blueprints upserted by guid, and derived tier/slot-option rows rebuilt per blueprint.
    /// </summary>
    Task<BlueprintImportCounts> ImportAsync(ParsedBlueprintDataset dataset, CancellationToken ct = default);

    /// <summary>Returns every blueprint with its computed display name, ordered by display name (FR-022).</summary>
    Task<IReadOnlyList<BlueprintListItemDto>> GetListAsync(CancellationToken ct = default);
}
