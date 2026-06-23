using Microsoft.Extensions.Logging;
using NajaEcho.Application.Abstractions;
using NajaEcho.Application.Features.Loot.GetMemberLedger;
using NajaEcho.Domain.Loot;

namespace NajaEcho.Application.Features.Loot.AddOrgPoints;

public sealed class AddOrgPointsHandler(ILootLedgerRepository repository, ILogger<AddOrgPointsHandler> logger)
{
    private const int DefaultLootPoints = 100;
    private const string DefaultLootReason = "Default";

    public async Task<LootLedgerEntry> HandleAsync(AddOrgPointsCommand command, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.Reason))
            throw new ArgumentException("Reason must not be empty.", nameof(command.Reason));

        var memberData = await repository.GetMemberLedgerAsync(command.MemberId, ct);
        if (memberData is null)
            throw new MemberNotFoundException(command.MemberId);

        var lootTotal = memberData.Entries
            .Where(e => e.Kind == nameof(LootLedgerKind.LootPoints))
            .Sum(e => e.Amount);

        if (lootTotal == 0)
        {
            var defaultEntry = new LootLedgerEntry
            {
                Id = Guid.NewGuid(),
                MemberId = command.MemberId,
                Kind = LootLedgerKind.LootPoints,
                Amount = DefaultLootPoints,
                Reason = DefaultLootReason,
                ActorId = command.ActorId,
                CreatedAt = DateTimeOffset.UtcNow,
            };

            await repository.AddEntryAsync(defaultEntry, ct);

            logger.LogInformation(
                "AddOrgPoints seeded default loot points memberId={MemberId} actorId={ActorId} amount={Amount}",
                command.MemberId, command.ActorId, DefaultLootPoints);
        }

        var entry = new LootLedgerEntry
        {
            Id = Guid.NewGuid(),
            MemberId = command.MemberId,
            Kind = LootLedgerKind.OrgPoints,
            Amount = command.Amount,
            Reason = command.Reason,
            ActorId = command.ActorId,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        await repository.AddEntryAsync(entry, ct);

        logger.LogInformation(
            "AddOrgPoints memberId={MemberId} actorId={ActorId} amount={Amount} outcome=succeeded",
            command.MemberId, command.ActorId, command.Amount);

        return entry;
    }
}
