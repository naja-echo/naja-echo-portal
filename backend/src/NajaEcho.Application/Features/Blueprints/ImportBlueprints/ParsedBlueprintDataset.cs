using NajaEcho.Domain.Blueprints;

namespace NajaEcho.Application.Features.Blueprints.ImportBlueprints;

/// <summary>
/// A blueprint slot option flattened for the query table, mutable so material resolution
/// (FR-009a) can be applied after parsing.
/// </summary>
public sealed class ParsedSlotOption
{
    public int SlotIndex { get; init; }
    public string SlotName { get; init; } = string.Empty;
    public int OptionIndex { get; init; }
    public CraftingMaterialKind Kind { get; init; }
    public string MaterialName { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public int MinQuality { get; init; }
    public int? MatchedUexId { get; set; }
}

/// <summary>A tier normalized for the query table, carrying its flattened slot options.</summary>
public sealed class ParsedTier
{
    public int TierIndex { get; init; }
    public int CraftTimeSeconds { get; init; }
    public IReadOnlyList<ParsedSlotOption> Options { get; init; } = [];
}

/// <summary>A validated blueprint: the entity (incl. tiers jsonb) plus its derived tier/option rows.</summary>
public sealed class ParsedBlueprint
{
    public CraftingBlueprint Entity { get; init; } = null!;
    public IReadOnlyList<ParsedTier> Tiers { get; init; } = [];
}

/// <summary>One rejected blueprint entry (FR-020).</summary>
public sealed record BlueprintRejection(string? Guid, string? ProductName, string Reason);

/// <summary>
/// The full parser output: validated blueprints with derived rows, reference data, the material
/// name lists, and per-entry rejections. Validation is complete before any DB write (Decision 7).
/// </summary>
public sealed class ParsedBlueprintDataset
{
    public string Version { get; init; } = string.Empty;
    public int MetaTotalBlueprints { get; init; }
    public int MetaTotalProducts { get; init; }
    public int MetaTotalResources { get; init; }
    public int MetaTotalItems { get; init; }
    public CraftingDataset Dataset { get; init; } = null!;
    public IReadOnlyList<CraftingProperty> Properties { get; init; } = [];
    public IReadOnlyList<string> ResourceNames { get; init; } = [];
    public IReadOnlyList<string> ItemNames { get; init; } = [];
    public List<CraftingMaterial> Materials { get; init; } = [];
    public IReadOnlyList<ParsedBlueprint> Blueprints { get; init; } = [];
    public IReadOnlyList<BlueprintRejection> Rejections { get; init; } = [];

    /// <summary>Number of entries present in the file's <c>blueprints</c> array (valid + rejected).</summary>
    public int BlueprintsRead { get; init; }

    /// <summary>Raw length of the file's <c>resources</c> array (before dropping malformed entries).</summary>
    public int RawResourceCount { get; init; }

    /// <summary>Raw length of the file's <c>items</c> array (before dropping malformed entries).</summary>
    public int RawItemCount { get; init; }
}
