namespace NajaEcho.Domain.Blueprints;

/// <summary>
/// A member's personal association with a catalog blueprint. One row per user/blueprint pair;
/// uniqueness is enforced by a DB constraint on (user_id, blueprint_id).
/// </summary>
public sealed class UserBlueprint
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid BlueprintId { get; set; }
    public DateTimeOffset AddedAt { get; set; }
}
