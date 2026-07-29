using System.Text.Json;

namespace NajaEcho.Domain.Blueprints;

/// <summary>
/// Single-row snapshot of the most recent uploaded dataset (FR-010; history out of scope). Holds
/// the dataset version, the <c>meta</c> totals as reported, and the dismantle configuration.
/// </summary>
public sealed class CraftingDataset
{
    public Guid Id { get; set; }
    public string Version { get; set; } = string.Empty;
    public int TotalBlueprints { get; set; }
    public int TotalProducts { get; set; }
    public int TotalResources { get; set; }
    public int TotalItems { get; set; }
    public double Efficiency { get; set; }
    public int DismantleTimeSeconds { get; set; }
    public JsonDocument BlacklistedResources { get; set; } = JsonDocument.Parse("[]");
    public JsonDocument BlacklistedEntityClasses { get; set; } = JsonDocument.Parse("[]");
    public DateTimeOffset ImportedAt { get; set; }
}
