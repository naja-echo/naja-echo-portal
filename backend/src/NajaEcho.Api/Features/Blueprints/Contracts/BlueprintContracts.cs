namespace NajaEcho.Api.Features.Blueprints.Contracts;

public sealed record BlueprintSlotOptionResponse(
    int OptionIndex,
    string MaterialName,
    string Kind,
    decimal Quantity);

public sealed record BlueprintSlotResponse(
    int SlotIndex,
    string SlotName,
    IReadOnlyList<BlueprintSlotOptionResponse> Options);

public sealed record BlueprintDetailResponse(
    Guid BlueprintId,
    string? ProductName,
    string? Type,
    int? CraftTimeSeconds,
    int IngredientCount,
    string? ComponentClass,
    int? ComponentSize,
    string? ComponentGrade,
    IReadOnlyList<BlueprintSlotResponse> Slots);

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
    string? Subtype,
    string? Gear,
    string? Tag,
    string? ComponentClass,
    int? ComponentSize,
    string? ComponentGrade,
    int IngredientCount);

public sealed record MyBlueprintListResponse(
    IReadOnlyList<MyBlueprintListItemResponse> Blueprints);

public sealed record AddMyBlueprintRequest(Guid BlueprintId);

public sealed record OrgBlueprintListItemResponse(
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

public sealed record OrgBlueprintListResponse(
    IReadOnlyList<OrgBlueprintListItemResponse> Blueprints);

public sealed record BlueprintOwnerResponse(Guid UserId, string DisplayName);

public sealed record OrgBlueprintDetailResponse(
    Guid BlueprintId,
    string? ProductName,
    string? Type,
    int? CraftTimeSeconds,
    int IngredientCount,
    string? ComponentClass,
    int? ComponentSize,
    string? ComponentGrade,
    IReadOnlyList<BlueprintSlotResponse> Slots,
    IReadOnlyList<BlueprintOwnerResponse> Owners);
