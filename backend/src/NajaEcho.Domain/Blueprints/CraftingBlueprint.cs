using System.Text.Json;

namespace NajaEcho.Domain.Blueprints;

/// <summary>
/// One craftable game product's blueprint. Stable identity is the file's <c>guid</c> (FR-012),
/// which also links to <c>sc.items.uuid</c> at read time when a match exists (FR-015). The full
/// nested tier/slot/option/modifier structure is stored in the <see cref="Tiers"/> jsonb column.
/// </summary>
public sealed class CraftingBlueprint
{
    public Guid Id { get; set; }
    public string Tag { get; set; } = string.Empty;
    public Guid ProductEntityClass { get; set; }
    public string Gear { get; set; } = string.Empty;
    public string? Type { get; set; }
    public string? Subtype { get; set; }
    public string? ProductName { get; set; }
    public string? Manufacturer { get; set; }
    public bool? IsDefault { get; set; }
    public string? SuggestedName { get; set; }
    public Guid? SuggestedProductEntityClass { get; set; }
    public bool? CigDataError { get; set; }
    public JsonDocument Tiers { get; set; } = JsonDocument.Parse("[]");
    public DateTimeOffset ImportedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
