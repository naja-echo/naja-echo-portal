namespace NajaEcho.Application.Features.Blueprints.GetMyBlueprints;

public sealed record MyBlueprintListItemDto(
    Guid BlueprintId,
    string? ProductName,
    string? Type,
    string? Subtype,
    int IngredientCount);
