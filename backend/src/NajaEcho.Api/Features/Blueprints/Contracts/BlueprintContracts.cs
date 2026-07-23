namespace NajaEcho.Api.Features.Blueprints.Contracts;

public sealed record BlueprintSearchResultResponse(
    Guid BlueprintId,
    string ProductName,
    string? Type);

public sealed record BlueprintSearchResponse(
    IReadOnlyList<BlueprintSearchResultResponse> Results);

public sealed record MyBlueprintListItemResponse(
    Guid BlueprintId,
    string? ProductName,
    string? Type,
    int IngredientCount);

public sealed record MyBlueprintListResponse(
    IReadOnlyList<MyBlueprintListItemResponse> Blueprints);

public sealed record AddMyBlueprintRequest(Guid BlueprintId);
