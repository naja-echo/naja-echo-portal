namespace NajaEcho.Application.Features.Blueprints.GetBlueprints;

/// <summary>
/// One row of the blueprint listing with its server-computed display name (FR-022). NameSource is
/// one of: <c>productName</c>, <c>linkedItem</c>, <c>tag</c>, <c>guid</c>.
/// </summary>
public sealed record BlueprintListItemDto(
    Guid Guid,
    string DisplayName,
    string NameSource,
    string? ProductName,
    string Tag,
    string? Manufacturer);
