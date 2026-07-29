namespace NajaEcho.Application.Features.Blueprints.GetBlueprintDetail;

public sealed record BlueprintDetailDto(
    Guid BlueprintId,
    string? ProductName,
    string? Type,
    int? CraftTimeSeconds,
    int IngredientCount,
    IReadOnlyList<BlueprintSlotDto> Slots);

public sealed record BlueprintSlotDto(
    int SlotIndex,
    string SlotName,
    IReadOnlyList<BlueprintSlotOptionDto> Options);

public sealed record BlueprintSlotOptionDto(
    int OptionIndex,
    string MaterialName,
    string Kind,
    decimal Quantity);
