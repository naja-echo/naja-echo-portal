namespace NajaEcho.Application.Features.Loot;

public static class ClaimPriority
{
    public static double Compute(int orgTotal, int lootTotal) =>
        (double)orgTotal / (lootTotal == 0 ? 100 : lootTotal);
}
