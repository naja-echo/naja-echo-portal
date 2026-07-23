namespace NajaEcho.Application.Features.Blueprints.SearchBlueprints;

public sealed record BlueprintSearchResultDto(
    Guid BlueprintId,
    string ProductName,
    string? Type);
