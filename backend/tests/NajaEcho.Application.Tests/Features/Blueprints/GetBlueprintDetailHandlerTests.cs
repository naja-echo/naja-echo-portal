using FluentAssertions;
using NajaEcho.Application.Abstractions;
using NajaEcho.Application.Features.Blueprints.GetBlueprintDetail;
using NajaEcho.Application.Features.Blueprints.GetMyBlueprints;
using Xunit;

namespace NajaEcho.Application.Tests.Features.Blueprints;

public class GetBlueprintDetailHandlerTests
{
    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

    private sealed class FakeRepo : IUserBlueprintRepository
    {
        private readonly Dictionary<(Guid, Guid), BlueprintDetailDto> _details = new();

        public void SeedDetail(Guid userId, BlueprintDetailDto detail) =>
            _details[(userId, detail.BlueprintId)] = detail;

        public Task<BlueprintDetailDto?> GetDetailAsync(Guid userId, Guid blueprintId, CancellationToken ct = default)
        {
            _details.TryGetValue((userId, blueprintId), out var detail);
            return Task.FromResult(detail);
        }

        public Task<IReadOnlyList<MyBlueprintListItemDto>> GetListAsync(Guid userId, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task<MyBlueprintListItemDto> AddAsync(Guid userId, Guid blueprintId, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task<bool> RemoveAsync(Guid userId, Guid blueprintId, CancellationToken ct = default) =>
            throw new NotImplementedException();
    }

    [Fact]
    public async Task Handle_BlueprintNotInUserList_ReturnsNull()
    {
        var repo = new FakeRepo();
        var handler = new GetBlueprintDetailHandler(repo);

        var result = await handler.HandleAsync(new GetBlueprintDetailQuery(UserId, Guid.NewGuid()));

        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_BlueprintInUserList_ReturnsCorrectFields()
    {
        var blueprintId = Guid.NewGuid();
        var detail = new BlueprintDetailDto(blueprintId, "Quantum Drive", "Component", 330, 5,
            [new BlueprintSlotDto(0, "Cast Iron", [new BlueprintSlotOptionDto(0, "Iron Ore", "material", 1.5m)])]);

        var repo = new FakeRepo();
        repo.SeedDetail(UserId, detail);
        var handler = new GetBlueprintDetailHandler(repo);

        var result = await handler.HandleAsync(new GetBlueprintDetailQuery(UserId, blueprintId));

        result.Should().NotBeNull();
        result!.BlueprintId.Should().Be(blueprintId);
        result.ProductName.Should().Be("Quantum Drive");
        result.Type.Should().Be("Component");
        result.CraftTimeSeconds.Should().Be(330);
        result.IngredientCount.Should().Be(5);
        result.Slots.Should().HaveCount(1);
        result.Slots[0].SlotName.Should().Be("Cast Iron");
        result.Slots[0].Options[0].MaterialName.Should().Be("Iron Ore");
    }

    [Fact]
    public async Task Handle_BlueprintWithNoSlots_ReturnsEmptySlotsCollection()
    {
        var blueprintId = Guid.NewGuid();
        var detail = new BlueprintDetailDto(blueprintId, "Widget", null, null, 0, []);

        var repo = new FakeRepo();
        repo.SeedDetail(UserId, detail);
        var handler = new GetBlueprintDetailHandler(repo);

        var result = await handler.HandleAsync(new GetBlueprintDetailQuery(UserId, blueprintId));

        result.Should().NotBeNull();
        result!.Slots.Should().BeEmpty();
    }
}
