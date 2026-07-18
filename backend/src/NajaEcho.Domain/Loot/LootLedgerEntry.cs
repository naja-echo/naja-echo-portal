namespace NajaEcho.Domain.Loot;

public sealed class LootLedgerEntry
{
    public Guid Id { get; set; }
    public Guid MemberId { get; set; }
    public LootLedgerKind Kind { get; set; }
    public int Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public Guid ActorId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
