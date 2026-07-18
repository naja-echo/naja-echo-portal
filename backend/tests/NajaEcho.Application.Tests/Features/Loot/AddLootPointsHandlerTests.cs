using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NajaEcho.Application.Abstractions;
using NajaEcho.Application.Features.Loot.AddLootPoints;
using NajaEcho.Application.Features.Loot.GetDistribution;
using NajaEcho.Application.Features.Loot.GetMemberLedger;
using NajaEcho.Domain.Loot;
using Xunit;

namespace NajaEcho.Application.Tests.Features.Loot;

public sealed class AddLootPointsHandlerTests
{
    private static readonly Guid KnownMemberId = Guid.NewGuid();

    private sealed class FakeLootRepo : ILootLedgerRepository
    {
        public bool MemberExists { get; set; } = true;
        public LootLedgerEntry? LastAdded { get; private set; }

        public Task AddEntryAsync(LootLedgerEntry entry, CancellationToken ct)
        {
            LastAdded = entry;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<DistributionRowDto>> GetDistributionAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<DistributionRowDto>>([]);

        public Task<MemberLedgerData?> GetMemberLedgerAsync(Guid memberId, CancellationToken ct) =>
            Task.FromResult(MemberExists ? (MemberLedgerData?)new MemberLedgerData(memberId, "Test", []) : null);
    }

    [Fact]
    public async Task EmptyReason_ThrowsArgumentException()
    {
        var repo = new FakeLootRepo();
        var handler = new AddLootPointsHandler(repo, NullLogger<AddLootPointsHandler>.Instance);
        var cmd = new AddLootPointsCommand(KnownMemberId, 50, "   ", Guid.NewGuid());

        await handler.Invoking(h => h.HandleAsync(cmd, default))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task UnknownMember_ThrowsMemberNotFoundException()
    {
        var repo = new FakeLootRepo { MemberExists = false };
        var handler = new AddLootPointsHandler(repo, NullLogger<AddLootPointsHandler>.Instance);
        var cmd = new AddLootPointsCommand(Guid.NewGuid(), 50, "Valid reason", Guid.NewGuid());

        await handler.Invoking(h => h.HandleAsync(cmd, default))
            .Should().ThrowAsync<MemberNotFoundException>();
    }

    [Fact]
    public async Task NegativeAmount_IsAccepted()
    {
        var repo = new FakeLootRepo();
        var handler = new AddLootPointsHandler(repo, NullLogger<AddLootPointsHandler>.Instance);
        var actorId = Guid.NewGuid();
        var cmd = new AddLootPointsCommand(KnownMemberId, -5, "Deduction", actorId);

        await handler.HandleAsync(cmd, default);

        repo.LastAdded.Should().NotBeNull();
        repo.LastAdded!.Amount.Should().Be(-5);
        repo.LastAdded.Kind.Should().Be(LootLedgerKind.LootPoints);
    }

    [Fact]
    public async Task ValidCommand_SetsLootPointsKind()
    {
        var repo = new FakeLootRepo();
        var handler = new AddLootPointsHandler(repo, NullLogger<AddLootPointsHandler>.Instance);
        var actorId = Guid.NewGuid();
        var cmd = new AddLootPointsCommand(KnownMemberId, 200, "Nice loot", actorId);

        await handler.HandleAsync(cmd, default);

        repo.LastAdded.Should().NotBeNull();
        repo.LastAdded!.MemberId.Should().Be(KnownMemberId);
        repo.LastAdded.ActorId.Should().Be(actorId);
        repo.LastAdded.Kind.Should().Be(LootLedgerKind.LootPoints);
        repo.LastAdded.Reason.Should().Be("Nice loot");
    }
}
