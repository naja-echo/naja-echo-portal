namespace NajaEcho.Application.Features.Blueprints.ImportBlueprints;

/// <summary>Per-collection counts (FR-019). Reference collections always report <c>Updated = 0</c>.</summary>
public sealed record CollectionCounts(int Read, int Inserted, int Updated, int Rejected);

/// <summary>The transient outcome of one upload (FR-019/020/021); not persisted (history out of scope).</summary>
public sealed record ImportBlueprintsResult(
    string Version,
    CollectionCounts Blueprints,
    CollectionCounts Resources,
    CollectionCounts Items,
    CollectionCounts Properties,
    bool ReferenceDataReplaced,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<BlueprintRejection> Rejections);
