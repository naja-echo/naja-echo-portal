namespace NajaEcho.Application.Features.Blueprints.GetMyBlueprints;

public sealed record MyBlueprintListItemDto(
    Guid BlueprintId,
    string? ProductName,
    string? Type,
    string? Subtype,
    string? Gear,
    string? Tag,
    string? ComponentClass,
    int? ComponentSize,
    string? ComponentGrade,
    int IngredientCount);
