using Microsoft.EntityFrameworkCore;
using NajaEcho.Application.Abstractions;
using NajaEcho.Application.Features.Blueprints.EnrichBlueprints;
using Npgsql;
using NpgsqlTypes;
using NajaEcho.Application.Features.Blueprints.GetBlueprints;
using NajaEcho.Application.Features.Blueprints.ImportBlueprints;
using NajaEcho.Application.Features.Blueprints.SearchBlueprints;
using NajaEcho.Domain.Blueprints;
using NajaEcho.Infrastructure.Persistence;

namespace NajaEcho.Infrastructure.Blueprints;

public sealed class BlueprintRepository(AppDbContext db) : IBlueprintRepository
{
    public async Task<IReadOnlyDictionary<string, MaterialMatch>> ResolveMaterialsAsync(
        IReadOnlyCollection<string> names, CancellationToken ct = default)
    {
        var lowered = names
            .Select(n => n.ToLowerInvariant())
            .Where(n => n.Length > 0)
            .Distinct()
            .ToList();

        var result = new Dictionary<string, MaterialMatch>();
        if (lowered.Count == 0)
        {
            return result;
        }

        // Match against sc.items only — exclude soft-deleted and unset-uex_id rows (research Decision 11).
        var itemRows = await db.Items
            .Where(i => i.SoftDeletedAt == null
                        && i.UexId > 0
                        && lowered.Contains(i.Name.ToLower()))
            .Select(i => new { i.Name, i.UexId })
            .ToListAsync(ct);

        foreach (var group in itemRows.GroupBy(r => r.Name.ToLowerInvariant()))
        {
            var winner = group.OrderBy(r => r.UexId).First();
            result[group.Key] = new MaterialMatch(winner.UexId);
        }

        return result;
    }

    public async Task<BlueprintImportCounts> ImportAsync(ParsedBlueprintDataset dataset, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var now = DateTimeOffset.UtcNow;

        // Reference data is replaced wholesale (FR-016).
        await db.CraftingMaterials.ExecuteDeleteAsync(ct);
        await db.CraftingProperties.ExecuteDeleteAsync(ct);
        await db.CraftingDatasets.ExecuteDeleteAsync(ct);

        dataset.Dataset.ImportedAt = now;
        db.CraftingDatasets.Add(dataset.Dataset);
        db.CraftingProperties.AddRange(dataset.Properties);
        db.CraftingMaterials.AddRange(dataset.Materials);

        // Upsert blueprints by guid (FR-013).
        var incomingIds = dataset.Blueprints.Select(b => b.Entity.Id).ToList();
        var existing = await db.Blueprints
            .Where(b => incomingIds.Contains(b.Id))
            .ToDictionaryAsync(b => b.Id, ct);

        // Rebuild derived rows: drop tiers for updated blueprints (cascade removes their options) → no stale rows (FR-014).
        if (existing.Count > 0)
        {
            var existingIds = existing.Keys.ToList();
            await db.BlueprintTiers.Where(t => existingIds.Contains(t.BlueprintId)).ExecuteDeleteAsync(ct);
        }

        int inserted = 0, updated = 0;

        foreach (var bp in dataset.Blueprints)
        {
            if (existing.TryGetValue(bp.Entity.Id, out var stored))
            {
                UpdateBlueprint(stored, bp.Entity, now);
                updated++;
            }
            else
            {
                bp.Entity.ImportedAt = now;
                bp.Entity.UpdatedAt = now;
                db.Blueprints.Add(bp.Entity);
                inserted++;
            }

            foreach (var tier in bp.Tiers)
            {
                var tierId = Guid.NewGuid();
                db.BlueprintTiers.Add(new CraftingBlueprintTier
                {
                    Id = tierId,
                    BlueprintId = bp.Entity.Id,
                    TierIndex = tier.TierIndex,
                    CraftTimeSeconds = tier.CraftTimeSeconds,
                });

                foreach (var option in tier.Options)
                {
                    db.BlueprintSlotOptions.Add(new CraftingBlueprintSlotOption
                    {
                        Id = Guid.NewGuid(),
                        TierId = tierId,
                        SlotIndex = option.SlotIndex,
                        SlotName = option.SlotName,
                        OptionIndex = option.OptionIndex,
                        Kind = option.Kind,
                        MaterialName = option.MaterialName,
                        Quantity = option.Quantity,
                        MinQuality = option.MinQuality,
                        MatchedUexId = option.MatchedUexId,
                    });
                }
            }
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new BlueprintImportCounts(inserted, updated);
    }

    public async Task<IReadOnlyList<BlueprintListItemDto>> GetListAsync(CancellationToken ct = default)
    {
        var blueprints = await db.Blueprints
            .Select(b => new { b.Id, b.ProductName, b.Tag, b.Manufacturer })
            .ToListAsync(ct);

        var idStrings = blueprints.Select(b => b.Id.ToString()).ToList();

        // Read-time link on items.uuid = blueprint guid, soft-deleted excluded, deterministic winner (Decision 5).
        var itemRows = await db.Items
            .Where(i => i.SoftDeletedAt == null && idStrings.Contains(i.Uuid))
            .Select(i => new { i.Uuid, i.Id, i.Name })
            .ToListAsync(ct);

        var itemNameByUuid = itemRows
            .GroupBy(i => i.Uuid)
            .ToDictionary(g => g.Key, g => g.OrderBy(i => i.Id).First().Name);

        var list = new List<BlueprintListItemDto>(blueprints.Count);
        foreach (var b in blueprints)
        {
            string displayName;
            string nameSource;

            if (!string.IsNullOrEmpty(b.ProductName))
            {
                displayName = b.ProductName;
                nameSource = "productName";
            }
            else if (itemNameByUuid.TryGetValue(b.Id.ToString(), out var itemName))
            {
                displayName = itemName;
                nameSource = "linkedItem";
            }
            else if (!string.IsNullOrEmpty(b.Tag))
            {
                displayName = b.Tag;
                nameSource = "tag";
            }
            else
            {
                displayName = b.Id.ToString();
                nameSource = "guid";
            }

            list.Add(new BlueprintListItemDto(b.Id, displayName, nameSource, b.ProductName, b.Tag, b.Manufacturer));
        }

        return list
            .OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<IReadOnlyList<BlueprintSearchResultDto>> SearchAsync(string term, int limit = 20, CancellationToken ct = default)
    {
        var escaped = term.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
        var pattern = $"%{escaped}%";

        var rows = await db.Blueprints
            .Where(b => b.ProductName != null && EF.Functions.ILike(b.ProductName, pattern, "\\"))
            .OrderBy(b => b.ProductName)
            .Take(limit)
            .Select(b => new BlueprintSearchResultDto(b.Id, b.ProductName!, b.Type))
            .ToListAsync(ct);

        return rows;
    }

    public async Task<int> EnrichAsync(IReadOnlyList<ParsedItemAttributes> items, CancellationToken ct = default)
    {
        if (items.Count == 0)
            return 0;

        // Pass four typed arrays to UNNEST — one round-trip regardless of item count.
        var entityClasses = items.Select(i => i.EntityClass).ToArray();
        var classes       = items.Select(i => i.ComponentClass).ToArray();
        var sizes         = items.Select(i => i.ComponentSize).ToArray();
        var grades        = items.Select(i => i.ComponentGrade).ToArray();

        var p0 = new NpgsqlParameter { Value = entityClasses, NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Uuid };
        var p1 = new NpgsqlParameter { Value = classes,       NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text };
        var p2 = new NpgsqlParameter { Value = sizes,         NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Smallint };
        var p3 = new NpgsqlParameter { Value = grades,        NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text };

        return await db.Database.ExecuteSqlRawAsync("""
            UPDATE sc.blueprints AS b
            SET
                component_class = v.component_class,
                component_size  = v.component_size,
                component_grade = v.component_grade
            FROM UNNEST({0}, {1}, {2}, {3}) AS v(entity_class, component_class, component_size, component_grade)
            WHERE b.product_entity_class = v.entity_class
            """, [p0, p1, p2, p3], ct);
    }

    private static void UpdateBlueprint(CraftingBlueprint stored, CraftingBlueprint inc, DateTimeOffset now)
    {
        stored.Tag = inc.Tag;
        stored.ProductEntityClass = inc.ProductEntityClass;
        stored.Gear = inc.Gear;
        stored.Type = inc.Type;
        stored.Subtype = inc.Subtype;
        stored.ProductName = inc.ProductName;
        stored.Manufacturer = inc.Manufacturer;
        stored.IsDefault = inc.IsDefault;
        stored.SuggestedName = inc.SuggestedName;
        stored.SuggestedProductEntityClass = inc.SuggestedProductEntityClass;
        stored.CigDataError = inc.CigDataError;
        stored.Tiers = inc.Tiers;
        stored.UpdatedAt = now;
    }
}
