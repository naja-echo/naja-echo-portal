namespace NajaEcho.Domain.Blueprints;

/// <summary>
/// The flat query record — one row per slot option (slots are flattened into their options,
/// FR-009b). Serves "all blueprints using material X" and material-aggregation queries. Carries
/// the <c>sc.items</c> <c>uex_id</c> resolved at import (FR-009a), null when unmatched. Modifiers
/// are NOT stored here (jsonb only).
/// </summary>
public sealed class CraftingBlueprintSlotOption
{
    public Guid Id { get; set; }
    public Guid TierId { get; set; }
    public int SlotIndex { get; set; }
    public string SlotName { get; set; } = string.Empty;
    public int OptionIndex { get; set; }
    public CraftingMaterialKind Kind { get; set; }
    public string MaterialName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public int MinQuality { get; set; }
    public int? MatchedUexId { get; set; }
}
