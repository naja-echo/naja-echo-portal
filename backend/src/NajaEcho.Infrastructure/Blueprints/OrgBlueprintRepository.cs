using Microsoft.EntityFrameworkCore;
using NajaEcho.Application.Abstractions;
using NajaEcho.Application.Features.Blueprints.GetBlueprintDetail;
using NajaEcho.Application.Features.Blueprints.GetOrgBlueprintDetail;
using NajaEcho.Application.Features.Blueprints.GetOrgBlueprints;
using NajaEcho.Infrastructure.Persistence;

namespace NajaEcho.Infrastructure.Blueprints;

public sealed class OrgBlueprintRepository(AppDbContext db) : IOrgBlueprintRepository
{
    private sealed record ListRow(Guid BlueprintId, string? ProductName, string? Type, int IngredientCount);
    private sealed record DetailHeaderRow(Guid BlueprintId, string? ProductName, string? Type, int? CraftTimeSeconds, int IngredientCount);
    private sealed record SlotOptionRow(int SlotIndex, string SlotName, int OptionIndex, string MaterialName, string Kind, decimal Quantity);
    private sealed record OwnerRow(Guid UserId, string DisplayName);

    public async Task<IReadOnlyList<OrgBlueprintListItemDto>> GetListAsync(Guid userId, CancellationToken ct = default)
    {
        var rows = await db.Database.SqlQuery<ListRow>($"""
            SELECT
              b.id                                    AS blueprint_id,
              b.product_name                          AS product_name,
              b.type                                  AS type,
              COUNT(DISTINCT bso.slot_index)::int     AS ingredient_count
            FROM user_blueprints ub
            JOIN sc.blueprints b ON b.id = ub.blueprint_id
            LEFT JOIN sc.blueprint_tiers bt ON bt.blueprint_id = b.id AND bt.tier_index = 0
            LEFT JOIN sc.blueprint_slot_options bso ON bso.tier_id = bt.id
            GROUP BY b.id, b.product_name, b.type
            ORDER BY b.product_name NULLS LAST, b.id
            """).ToListAsync(ct);

        return rows
            .Select(r => new OrgBlueprintListItemDto(r.BlueprintId, r.ProductName, r.Type, r.IngredientCount))
            .ToList();
    }

    public async Task<OrgBlueprintDetailDto?> GetDetailAsync(Guid userId, Guid blueprintId, CancellationToken ct = default)
    {
        // Verify at least one user owns this blueprint and fetch header fields
        var header = await db.Database.SqlQuery<DetailHeaderRow>($"""
            SELECT
              b.id                                    AS blueprint_id,
              b.product_name                          AS product_name,
              b.type                                  AS type,
              bt.craft_time_seconds                   AS craft_time_seconds,
              COUNT(DISTINCT bso.slot_index)::int     AS ingredient_count
            FROM user_blueprints ub
            JOIN sc.blueprints b ON b.id = ub.blueprint_id
            LEFT JOIN sc.blueprint_tiers bt ON bt.blueprint_id = b.id AND bt.tier_index = 0
            LEFT JOIN sc.blueprint_slot_options bso ON bso.tier_id = bt.id
            WHERE ub.blueprint_id = {blueprintId}
            GROUP BY b.id, b.product_name, b.type, bt.craft_time_seconds
            """).FirstOrDefaultAsync(ct);

        if (header is null)
            return null;

        // Fetch ordered slot options
        var optionRows = await db.Database.SqlQuery<SlotOptionRow>($"""
            SELECT
              bso.slot_index      AS slot_index,
              bso.slot_name       AS slot_name,
              bso.option_index    AS option_index,
              bso.material_name   AS material_name,
              bso.kind            AS kind,
              bso.quantity        AS quantity
            FROM sc.blueprint_slot_options bso
            JOIN sc.blueprint_tiers bt ON bt.id = bso.tier_id
            WHERE bt.blueprint_id = {blueprintId}
              AND bt.tier_index = 0
            ORDER BY bso.slot_index, bso.option_index
            """).ToListAsync(ct);

        var slots = optionRows
            .GroupBy(r => r.SlotIndex)
            .OrderBy(g => g.Key)
            .Select(g => new BlueprintSlotDto(
                g.Key,
                g.First().SlotName,
                g.Select(r => new BlueprintSlotOptionDto(r.OptionIndex, r.MaterialName, r.Kind, r.Quantity))
                 .ToList()))
            .ToList();

        // Fetch all users who own this blueprint
        var ownerRows = await db.Database.SqlQuery<OwnerRow>($"""
            SELECT
              u.id                                                                  AS user_id,
              CASE WHEN u.display_name <> '' THEN u.display_name ELSE u.user_name END AS display_name
            FROM user_blueprints ub
            JOIN "AspNetUsers" u ON u.id = ub.user_id
            WHERE ub.blueprint_id = {blueprintId}
            ORDER BY display_name
            """).ToListAsync(ct);

        var owners = ownerRows
            .Select(r => new OrgBlueprintOwnerDto(r.UserId, r.DisplayName))
            .ToList();

        return new OrgBlueprintDetailDto(
            header.BlueprintId,
            header.ProductName,
            header.Type,
            header.CraftTimeSeconds,
            header.IngredientCount,
            slots,
            owners);
    }
}
