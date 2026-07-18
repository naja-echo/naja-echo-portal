namespace NajaEcho.Domain.Blueprints;

/// <summary>
/// A crafting-material name from the dataset's <c>resources</c> or <c>items</c> list, with its
/// catalog resolution snapshot (FR-009). Composite identity is (<see cref="Kind"/>,
/// <see cref="Name"/>) — one polymorphic table (019 <c>loot_ledger</c> precedent). Names resolve
/// against <c>sc.items</c> by <c>uex_id</c> at import; null when unmatched.
/// </summary>
public sealed class CraftingMaterial
{
    public CraftingMaterialKind Kind { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? MatchedUexId { get; set; }
}
