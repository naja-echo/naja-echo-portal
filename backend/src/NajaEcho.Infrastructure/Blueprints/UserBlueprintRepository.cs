using Microsoft.EntityFrameworkCore;
using NajaEcho.Application.Abstractions;
using NajaEcho.Application.Features.Blueprints.GetMyBlueprints;
using NajaEcho.Domain.Blueprints;
using NajaEcho.Infrastructure.Persistence;

namespace NajaEcho.Infrastructure.Blueprints;

public sealed class UserBlueprintRepository(AppDbContext db) : IUserBlueprintRepository
{
    private sealed record ListRow(Guid BlueprintId, string? ProductName, string? Type, int IngredientCount);

    public async Task<IReadOnlyList<MyBlueprintListItemDto>> GetListAsync(Guid userId, CancellationToken ct = default)
    {
        var rows = await db.Database.SqlQuery<ListRow>($"""
            SELECT
              b.id                                                         AS blueprint_id,
              b.product_name                                               AS product_name,
              b.type                                                       AS type,
              COUNT(DISTINCT bso.slot_index)::int                         AS ingredient_count
            FROM user_blueprints ub
            JOIN sc.blueprints b ON b.id = ub.blueprint_id
            LEFT JOIN sc.blueprint_tiers bt ON bt.blueprint_id = b.id AND bt.tier_index = 0
            LEFT JOIN sc.blueprint_slot_options bso ON bso.tier_id = bt.id
            WHERE ub.user_id = {userId}
            GROUP BY b.id, b.product_name, b.type
            ORDER BY b.product_name NULLS LAST, b.id
            """).ToListAsync(ct);

        return rows
            .Select(r => new MyBlueprintListItemDto(r.BlueprintId, r.ProductName, r.Type, r.IngredientCount))
            .ToList();
    }

    public async Task<MyBlueprintListItemDto> AddAsync(Guid userId, Guid blueprintId, CancellationToken ct = default)
    {
        var blueprint = await db.Blueprints
            .Where(b => b.Id == blueprintId)
            .Select(b => new { b.Id, b.ProductName, b.Type })
            .FirstOrDefaultAsync(ct)
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

        return new MyBlueprintListItemDto(blueprint.Id, blueprint.ProductName, blueprint.Type, ingredientCount);
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex) =>
        ex.InnerException?.Message.Contains("ux_user_blueprints_user_blueprint",
            StringComparison.OrdinalIgnoreCase) == true
        || ex.InnerException?.Message.Contains("23505", StringComparison.OrdinalIgnoreCase) == true;
}
