using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NajaEcho.Application.Abstractions;
using NajaEcho.Application.Features.Loot.AddOrgPoints;
using NajaEcho.Application.Features.Loot.GetDistribution;
using NajaEcho.Application.Features.Loot.GetMemberLedger;
using NajaEcho.Domain.Loot;
using Xunit;

namespace NajaEcho.Application.Tests.Features.Loot;

public sealed class AddOrgPointsHandlerTests
{
    private static readonly Guid KnownMemberId = Guid.NewGuid();

    private sealed class FakeLootRepo : ILootLedgerRepository
    {
        public bool MemberExists { get; set; } = true;
        public List<LedgerEntryDto> ExistingEntries { get; } = [];
        public List<LootLedgerEntry> Added { get; } = [];
        public LootLedgerEntry? LastAdded => Added.Count > 0 ? Added[^1] : null;

        public Task AddEntryAsync(LootLedgerEntry entry, CancellationToken ct)
        {
            Added.Add(entry);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<DistributionRowDto>> GetDistributionAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<DistributionRowDto>>([]);

        public Task<MemberLedgerData?> GetMemberLedgerAsync(Guid memberId, CancellationToken ct) =>
            Task.FromResult(MemberExists ? (MemberLedgerData?)new MemberLedgerData(memberId, "Test", ExistingEntries) : null);
    }

    [Fact]
    public async Task EmptyReason_ThrowsArgumentException()
    {
        var repo = new FakeLootRepo();
        var handler = new AddOrgPointsHandler(repo, NullLogger<AddOrgPointsHandler>.Instance);
        var cmd = new AddOrgPointsCommand(KnownMemberId, 50, "   ", Guid.NewGuid());

        await handler.Invoking(h => h.HandleAsync(cmd, default))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task UnknownMember_ThrowsMemberNotFoundException()
    {
        var repo = new FakeLootRepo { MemberExists = false };
        var handler = new AddOrgPointsHandler(repo, NullLogger<AddOrgPointsHandler>.Instance);
        var cmd = new AddOrgPointsCommand(Guid.NewGuid(), 50, "Valid reason", Guid.NewGuid());

        await handler.Invoking(h => h.HandleAsync(cmd, default))
            .Should().ThrowAsync<MemberNotFoundException>();
    }

    [Fact]
    public async Task NegativeAmount_IsAccepted()
    {
        var repo = new FakeLootRepo();
        var handler = new AddOrgPointsHandler(repo, NullLogger<AddOrgPointsHandler>.Instance);
        var actorId = Guid.NewGuid();
        var cmd = new AddOrgPointsCommand(KnownMemberId, -10, "Correction", actorId);

        await handler.HandleAsync(cmd, default);

        repo.LastAdded.Should().NotBeNull();
        repo.LastAdded!.Amount.Should().Be(-10);
        repo.LastAdded.Kind.Should().Be(LootLedgerKind.OrgPoints);
    }

    [Fact]
    public async Task ValidCommand_SetsCorrectFields()
    {
        var repo = new FakeLootRepo();
        var handler = new AddOrgPointsHandler(repo, NullLogger<AddOrgPointsHandler>.Instance);
        var actorId = Guid.NewGuid();
        var cmd = new AddOrgPointsCommand(KnownMemberId, 100, "Great contribution", actorId);

        await handler.HandleAsync(cmd, default);

        repo.LastAdded.Should().NotBeNull();
        repo.LastAdded!.MemberId.Should().Be(KnownMemberId);
        repo.LastAdded.ActorId.Should().Be(actorId);
        repo.LastAdded.Kind.Should().Be(LootLedgerKind.OrgPoints);
        repo.LastAdded.Reason.Should().Be("Great contribution");
    }

    [Fact]
    public async Task MemberWithZeroLootPoints_SeedsDefaultLootEntry()
    {
        var repo = new FakeLootRepo();
        var handler = new AddOrgPointsHandler(repo, NullLogger<AddOrgPointsHandler>.Instance);
        var actorId = Guid.NewGuid();
        var cmd = new AddOrgPointsCommand(KnownMemberId, 50, "Great contribution", actorId);

        var result = await handler.HandleAsync(cmd, default);

        repo.Added.Should().HaveCount(2);
        var defaultEntry = repo.Added.Should().ContainSingle(e =>
            e.Kind == LootLedgerKind.LootPoints && e.Amount == 100 && e.Reason == "Default").Subject;
        defaultEntry.MemberId.Should().Be(KnownMemberId);
        defaultEntry.ActorId.Should().Be(actorId);

        // The returned entry is the requested org-points entry.
        result.Kind.Should().Be(LootLedgerKind.OrgPoints);
        result.Amount.Should().Be(50);
    }

    [Fact]
    public async Task MemberWithExistingLootPoints_DoesNotSeedDefault()
    {
        var repo = new FakeLootRepo();
        repo.ExistingEntries.Add(new LedgerEntryDto(
            Guid.NewGuid(), nameof(LootLedgerKind.LootPoints), 25, "Prior award", "someone", DateTimeOffset.UtcNow));
        var handler = new AddOrgPointsHandler(repo, NullLogger<AddOrgPointsHandler>.Instance);
        var cmd = new AddOrgPointsCommand(KnownMemberId, 50, "Great contribution", Guid.NewGuid());

        await handler.HandleAsync(cmd, default);

        repo.Added.Should().ContainSingle();
        repo.Added[0].Kind.Should().Be(LootLedgerKind.OrgPoints);
    }
}
