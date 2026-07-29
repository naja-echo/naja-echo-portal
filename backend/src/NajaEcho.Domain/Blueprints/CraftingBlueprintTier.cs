namespace NajaEcho.Domain.Blueprints;

/// <summary>
/// A blueprint tier normalized into its own queryable record (FR-009b). Derived from the same
/// parsed data as the blueprint's <c>tiers</c> jsonb and rebuilt on every insert/update.
/// </summary>
public sealed class CraftingBlueprintTier
{
    public Guid Id { get; set; }
    public Guid BlueprintId { get; set; }
    public int TierIndex { get; set; }
    public int CraftTimeSeconds { get; set; }
}
