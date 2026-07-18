namespace NajaEcho.Application.Features.Loot.AddOrgPoints;

public sealed record AddOrgPointsCommand(Guid MemberId, int Amount, string Reason, Guid ActorId);
