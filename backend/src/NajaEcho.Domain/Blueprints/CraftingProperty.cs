using System.Text.Json;

namespace NajaEcho.Domain.Blueprints;

/// <summary>
/// A property-catalog entry keyed by <c>propertyKey</c> (FR-008). Modifier <c>propertyKey</c>
/// values inside blueprint <c>tiers</c> reference these keys informally — never enforced.
/// </summary>
public sealed class CraftingProperty
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Unit { get; set; }
    public string Category { get; set; } = string.Empty;
    public JsonDocument? NameOverrides { get; set; }
}
