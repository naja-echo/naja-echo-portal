using Microsoft.Extensions.Logging;
using NajaEcho.Application.Abstractions;
using NajaEcho.Application.Features.Loot.GetMemberLedger;
using NajaEcho.Domain.Loot;

namespace NajaEcho.Application.Features.Loot.AddLootPoints;

public sealed class AddLootPointsHandler(ILootLedgerRepository repository, ILogger<AddLootPointsHandler> logger)
{
    public async Task<LootLedgerEntry> HandleAsync(AddLootPointsCommand command, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.Reason))
            throw new ArgumentException("Reason must not be empty.", nameof(command.Reason));

        var memberData = await repository.GetMemberLedgerAsync(command.MemberId, ct);
        if (memberData is null)
            throw new MemberNotFoundException(command.MemberId);

        var entry = new LootLedgerEntry
        {
            Id = Guid.NewGuid(),
            MemberId = command.MemberId,
            Kind = LootLedgerKind.LootPoints,
            Amount = command.Amount,
            Reason = command.Reason,
            ActorId = command.ActorId,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        await repository.AddEntryAsync(entry, ct);

        logger.LogInformation(
            "AddLootPoints memberId={MemberId} actorId={ActorId} amount={Amount} outcome=succeeded",
            command.MemberId, command.ActorId, command.Amount);

        return entry;
    }
}
