namespace NajaEcho.Application.Features.Blueprints.GetOrgBlueprints;

public sealed record OrgBlueprintListItemDto(
    Guid BlueprintId,
    string? ProductName,
    string? Type,
    string? Subtype,
    int IngredientCount);
