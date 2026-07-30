using NajaEcho.Application.Features.Blueprints.GetBlueprintDetail;

namespace NajaEcho.Application.Features.Blueprints.GetOrgBlueprintDetail;

public sealed record OrgBlueprintOwnerDto(Guid UserId, string DisplayName);

public sealed record OrgBlueprintDetailDto(
    Guid BlueprintId,
    string? ProductName,
    string? Type,
    int? CraftTimeSeconds,
    int IngredientCount,
    string? ComponentClass,
    int? ComponentSize,
    string? ComponentGrade,
    IReadOnlyList<BlueprintSlotDto> Slots,
    IReadOnlyList<OrgBlueprintOwnerDto> Owners);
