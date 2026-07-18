namespace NajaEcho.Application.Features.Loot.AddLootPoints;

public sealed record AddLootPointsCommand(Guid MemberId, int Amount, string Reason, Guid ActorId);
