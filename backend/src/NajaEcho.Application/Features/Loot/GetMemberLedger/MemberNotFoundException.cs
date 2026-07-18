namespace NajaEcho.Application.Features.Loot.GetMemberLedger;

public sealed class MemberNotFoundException(Guid memberId)
    : Exception($"Member {memberId} not found.");
