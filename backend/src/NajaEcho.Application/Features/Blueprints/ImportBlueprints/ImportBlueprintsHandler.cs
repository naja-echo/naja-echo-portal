using Microsoft.Extensions.Logging;
using NajaEcho.Application.Abstractions;
using NajaEcho.Application.Features.Ships.ImportShips;

namespace NajaEcho.Application.Features.Blueprints.ImportBlueprints;

public sealed class ImportBlueprintsHandler(
    IBlueprintRepository repository,
    IImportCoordinator coordinator,
    ILogger<ImportBlueprintsHandler> logger)
{
    public async Task<ImportBlueprintsResult> HandleAsync(ImportBlueprintsCommand command, CancellationToken ct = default)
    {
        if (!coordinator.TryAcquire())
        {
            logger.LogWarning("ImportBlueprints: already in progress");
            throw new ImportAlreadyInProgressException();
        }

        var startedAt = DateTimeOffset.UtcNow;

        try
        {
            var dataset = BlueprintParser.Parse(command.Document);

            // Resolve every distinct material name referenced anywhere (lists + options) once.
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var n in dataset.ResourceNames)
            {
                names.Add(n);
            }

            foreach (var n in dataset.ItemNames)
            {
                names.Add(n);
            }

            foreach (var bp in dataset.Blueprints)
            {
                foreach (var tier in bp.Tiers)
                {
                    foreach (var option in tier.Options)
                    {
                        names.Add(option.MaterialName);
                    }
                }
            }

            var map = await repository.ResolveMaterialsAsync(names, ct);

            ApplyMaterialMatches(dataset, map);

            var warnings = BuildWarnings(dataset, map);

            var counts = await repository.ImportAsync(dataset, ct);

            var result = new ImportBlueprintsResult(
                dataset.Version,
                new CollectionCounts(dataset.BlueprintsRead, counts.BlueprintsInserted, counts.BlueprintsUpdated, dataset.Rejections.Count),
                new CollectionCounts(dataset.ResourceNames.Count, dataset.ResourceNames.Count, 0, 0),
                new CollectionCounts(dataset.ItemNames.Count, dataset.ItemNames.Count, 0, 0),
                new CollectionCounts(dataset.Properties.Count, dataset.Properties.Count, 0, 0),
                ReferenceDataReplaced: true,
                warnings,
                dataset.Rejections);

            var durationMs = (long)(DateTimeOffset.UtcNow - startedAt).TotalMilliseconds;
            logger.LogInformation(
                "ImportBlueprints: completed — version={Version} blueprints(ins={Ins} upd={Upd} rej={Rej}) resources={Res} items={Items} properties={Props} warnings={Warn} durationMs={Duration}",
                dataset.Version, counts.BlueprintsInserted, counts.BlueprintsUpdated, dataset.Rejections.Count,
                dataset.ResourceNames.Count, dataset.ItemNames.Count, dataset.Properties.Count, warnings.Count, durationMs);

            return result;
        }
        catch (InvalidBlueprintDocumentException ex)
        {
            logger.LogWarning("ImportBlueprints: rejected invalid document — {Reason}", ex.Message);
            throw;
        }
        catch (ImportAlreadyInProgressException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ImportBlueprints: unhandled failure — rolled back");
            throw;
        }
        finally
        {
            coordinator.Release();
        }
    }

    private static void ApplyMaterialMatches(
        ParsedBlueprintDataset dataset, IReadOnlyDictionary<string, MaterialMatch> map)
    {
        foreach (var material in dataset.Materials)
        {
            if (map.TryGetValue(material.Name.ToLowerInvariant(), out var match))
            {
                material.MatchedUexId = match.UexId;
            }
        }

        foreach (var bp in dataset.Blueprints)
        {
            foreach (var tier in bp.Tiers)
            {
                foreach (var option in tier.Options)
                {
                    if (map.TryGetValue(option.MaterialName.ToLowerInvariant(), out var match))
                    {
                        option.MatchedUexId = match.UexId;
                    }
                }
            }
        }
    }

    private static List<string> BuildWarnings(
        ParsedBlueprintDataset dataset, IReadOnlyDictionary<string, MaterialMatch> map)
    {
        var warnings = new List<string>();

        if (dataset.MetaTotalBlueprints != dataset.BlueprintsRead)
        {
            warnings.Add($"meta.totalBlueprints is {dataset.MetaTotalBlueprints} but {dataset.BlueprintsRead} blueprint entries were parsed.");
        }

        // Compare meta totals against the raw array lengths so the cross-check stays faithful to the
        // file even when malformed (null/empty/non-string) entries were dropped during parsing.
        if (dataset.MetaTotalResources != dataset.RawResourceCount)
        {
            warnings.Add($"meta.totalResources is {dataset.MetaTotalResources} but {dataset.RawResourceCount} resource entries were present.");
        }

        if (dataset.MetaTotalItems != dataset.RawItemCount)
        {
            warnings.Add($"meta.totalItems is {dataset.MetaTotalItems} but {dataset.RawItemCount} item entries were present.");
        }

        // Surface dropped malformed entries so a shorter parsed list is explained (not silent).
        var droppedResources = dataset.RawResourceCount - dataset.ResourceNames.Count;
        if (droppedResources > 0)
        {
            warnings.Add($"{droppedResources} resource entr{(droppedResources == 1 ? "y was" : "ies were")} empty or not a string and skipped.");
        }

        var droppedItems = dataset.RawItemCount - dataset.ItemNames.Count;
        if (droppedItems > 0)
        {
            warnings.Add($"{droppedItems} item entr{(droppedItems == 1 ? "y was" : "ies were")} empty or not a string and skipped.");
        }

        // FR-009: unmatched crafting-material names (from the resources/items lists) warn, never reject.
        foreach (var material in dataset.Materials)
        {
            if (!map.ContainsKey(material.Name.ToLowerInvariant()))
            {
                warnings.Add($"material '{material.Name}' matched no item; stored unlinked.");
            }
        }

        // Dedupe: the same name can appear as both a resource and an item material, producing an
        // identical unmatched warning twice.
        return warnings.Distinct().ToList();
    }
}
