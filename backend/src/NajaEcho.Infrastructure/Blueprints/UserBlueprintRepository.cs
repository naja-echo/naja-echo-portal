using Microsoft.EntityFrameworkCore;
using Npgsql;
using NajaEcho.Application.Abstractions;
using NajaEcho.Application.Features.Blueprints.GetBlueprintDetail;
using NajaEcho.Application.Features.Blueprints.GetMyBlueprints;
using NajaEcho.Domain.Blueprints;
using NajaEcho.Infrastructure.Persistence;

namespace NajaEcho.Infrastructure.Blueprints;

public sealed class UserBlueprintRepository(AppDbContext db) : IUserBlueprintRepository
{
    private sealed record ListRow(Guid BlueprintId, string? ProductName, string? Type, string? Subtype, string? Gear, string? Tag, string? ComponentClass, int? ComponentSize, string? ComponentGrade, int IngredientCount);
    private sealed record DetailHeaderRow(Guid BlueprintId, string? ProductName, string? Type, int? CraftTimeSeconds, string? ComponentClass, int? ComponentSize, string? ComponentGrade, int IngredientCount);
    private sealed record AddBlueprintRow(Guid Id, string? ProductName, string? Type, string? Subtype, string? Gear, string? Tag, string? ComponentClass, int? ComponentSize, string? ComponentGrade);
    private sealed record SlotOptionRow(int SlotIndex, string SlotName, int OptionIndex, string MaterialName, string Kind, decimal Quantity);

    public async Task<IReadOnlyList<MyBlueprintListItemDto>> GetListAsync(Guid userId, CancellationToken ct = default)
    {
        var rows = await db.Database.SqlQuery<ListRow>($"""
            SELECT
              b.id                                                         AS blueprint_id,
              b.product_name                                               AS product_name,
              b.type                                                       AS type,
              b.subtype                                                    AS subtype,
              b.gear                                                       AS gear,
              b.tag                                                        AS tag,
              COALESCE(b.component_class, sca.class)                      AS component_class,
              COALESCE(b.component_size, sca.size, CASE WHEN b.subtype ~ '^size\d+$' THEN substring(b.subtype FROM 5)::int END) AS component_size,
              COALESCE(b.component_grade, sca.grade)                      AS component_grade,
              COUNT(DISTINCT bso.slot_index)::int                         AS ingredient_count
            FROM user_blueprints ub
            JOIN sc.blueprints b ON b.id = ub.blueprint_id
            LEFT JOIN sc.items i ON i.uuid = b.product_entity_class::text
            LEFT JOIN sc.ship_component_attributes sca ON sca.item_id = i.id
            LEFT JOIN sc.blueprint_tiers bt ON bt.blueprint_id = b.id AND bt.tier_index = 0
            LEFT JOIN sc.blueprint_slot_options bso ON bso.tier_id = bt.id
            WHERE ub.user_id = {userId}
            GROUP BY b.id, b.product_name, b.type, b.subtype, b.gear, b.tag, b.component_class, b.component_size, b.component_grade, sca.class, sca.size, sca.grade
            ORDER BY b.product_name NULLS LAST, b.id
            """).ToListAsync(ct);

        return rows
            .Select(r => new MyBlueprintListItemDto(r.BlueprintId, r.ProductName, r.Type, r.Subtype, r.Gear, r.Tag, r.ComponentClass, r.ComponentSize, r.ComponentGrade, r.IngredientCount))
            .ToList();
    }

    public async Task<MyBlueprintListItemDto> AddAsync(Guid userId, Guid blueprintId, CancellationToken ct = default)
    {
        var blueprint = await db.Database.SqlQuery<AddBlueprintRow>($"""
            SELECT
              b.id                                   AS id,
              b.product_name                         AS product_name,
              b.type                                 AS type,
              b.subtype                              AS subtype,
              b.gear                                 AS gear,
              b.tag                                  AS tag,
              COALESCE(b.component_class, sca.class) AS component_class,
              COALESCE(b.component_size, sca.size, CASE WHEN b.subtype ~ '^size\d+$' THEN substring(b.subtype FROM 5)::int END) AS component_size,
              COALESCE(b.component_grade, sca.grade) AS component_grade
            FROM sc.blueprints b
            LEFT JOIN sc.items i ON i.uuid = b.product_entity_class::text
            LEFT JOIN sc.ship_component_attributes sca ON sca.item_id = i.id
            WHERE b.id = {blueprintId}
            """).FirstOrDefaultAsync(ct)
            ?? throw new BlueprintNotFoundException(blueprintId);

        var entry = new UserBlueprint
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            BlueprintId = blueprintId,
            AddedAt = DateTimeOffset.UtcNow,
        };

        db.UserBlueprints.Add(entry);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            throw new DuplicateBlueprintException(blueprintId);
        }

        var ingredientCount = await db.Database.SqlQuery<int>($"""
            SELECT COUNT(DISTINCT bso.slot_index)::int AS "Value"
            FROM sc.blueprint_slot_options bso
            JOIN sc.blueprint_tiers bt ON bt.id = bso.tier_id
            WHERE bt.blueprint_id = {blueprintId}
              AND bt.tier_index = 0
            """).FirstOrDefaultAsync(ct);

        return new MyBlueprintListItemDto(blueprint.Id, blueprint.ProductName, blueprint.Type, blueprint.Subtype, blueprint.Gear, blueprint.Tag, blueprint.ComponentClass, blueprint.ComponentSize, blueprint.ComponentGrade, ingredientCount);
    }

    public async Task<BlueprintDetailDto?> GetDetailAsync(Guid userId, Guid blueprintId, CancellationToken ct = default)
    {
        // Verify ownership and fetch header fields
        var header = await db.Database.SqlQuery<DetailHeaderRow>($"""
            SELECT
              b.id                                                AS blueprint_id,
              b.product_name                                      AS product_name,
              b.type                                              AS type,
              bt.craft_time_seconds                               AS craft_time_seconds,
              COALESCE(b.component_class, sca.class)              AS component_class,
              COALESCE(b.component_size, sca.size, CASE WHEN b.subtype ~ '^size\d+$' THEN substring(b.subtype FROM 5)::int END) AS component_size,
              COALESCE(b.component_grade, sca.grade)              AS component_grade,
              COUNT(DISTINCT bso.slot_index)::int                AS ingredient_count
            FROM user_blueprints ub
            JOIN sc.blueprints b ON b.id = ub.blueprint_id
            LEFT JOIN sc.items i ON i.uuid = b.product_entity_class::text
            LEFT JOIN sc.ship_component_attributes sca ON sca.item_id = i.id
            LEFT JOIN sc.blueprint_tiers bt ON bt.blueprint_id = b.id AND bt.tier_index = 0
            LEFT JOIN sc.blueprint_slot_options bso ON bso.tier_id = bt.id
            WHERE ub.user_id = {userId}
              AND ub.blueprint_id = {blueprintId}
            GROUP BY b.id, b.product_name, b.type, bt.craft_time_seconds, b.subtype, b.component_class, b.component_size, b.component_grade, sca.class, sca.size, sca.grade
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

        return new BlueprintDetailDto(
            header.BlueprintId,
            header.ProductName,
            header.Type,
            header.CraftTimeSeconds,
            header.IngredientCount,
            header.ComponentClass,
            header.ComponentSize,
            header.ComponentGrade,
            slots);
    }

    public async Task<bool> RemoveAsync(Guid userId, Guid blueprintId, CancellationToken ct = default)
    {
        var entity = await db.UserBlueprints
            .FirstOrDefaultAsync(ub => ub.UserId == userId && ub.BlueprintId == blueprintId, ct);

        if (entity is null)
            return false;

        db.UserBlueprints.Remove(entity);
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex) =>
        ex.InnerException is Npgsql.PostgresException { SqlState: "23505" };
}
