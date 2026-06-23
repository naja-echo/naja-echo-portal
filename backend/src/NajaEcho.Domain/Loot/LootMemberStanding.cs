namespace NajaEcho.Domain.Loot;

public sealed class LootMemberStanding
{
    public Guid MemberId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public int OrgPointsTotal { get; set; }
    public int LootPointsTotal { get; set; }
    public double ClaimPriority { get; set; }
}
