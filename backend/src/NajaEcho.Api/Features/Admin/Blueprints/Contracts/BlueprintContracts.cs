namespace NajaEcho.Api.Features.Admin.Blueprints.Contracts;

public sealed record CollectionCountsResponse(int Read, int Inserted, int Updated, int Rejected);

public sealed record BlueprintRejectionResponse(string? Guid, string? ProductName, string Reason);

public sealed record ImportBlueprintsResponse(
    string Version,
    CollectionCountsResponse Blueprints,
    CollectionCountsResponse Resources,
    CollectionCountsResponse Items,
    CollectionCountsResponse Properties,
    bool ReferenceDataReplaced,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<BlueprintRejectionResponse> Rejections);

public sealed record BlueprintListItemResponse(
    Guid Guid,
    string DisplayName,
    string NameSource,
    string? ProductName,
    string Tag,
    string? Manufacturer);

public sealed record BlueprintListResponse(IReadOnlyList<BlueprintListItemResponse> Blueprints);

public sealed record EnrichBlueprintsResponse(int ItemsParsed, int BlueprintsUpdated);
