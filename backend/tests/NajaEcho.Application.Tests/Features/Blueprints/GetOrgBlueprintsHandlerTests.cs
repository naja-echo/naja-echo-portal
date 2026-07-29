using FluentAssertions;
using NajaEcho.Application.Abstractions;
using NajaEcho.Application.Features.Blueprints.GetOrgBlueprintDetail;
using NajaEcho.Application.Features.Blueprints.GetOrgBlueprints;
using Xunit;

namespace NajaEcho.Application.Tests.Features.Blueprints;

public class GetOrgBlueprintsHandlerTests
{
    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

    private sealed class FakeRepo : IOrgBlueprintRepository
    {
        private IReadOnlyList<OrgBlueprintListItemDto> _list = [];

        public void SeedList(IReadOnlyList<OrgBlueprintListItemDto> items) => _list = items;

        public Task<IReadOnlyList<OrgBlueprintListItemDto>> GetListAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult(_list);

        public Task<OrgBlueprintDetailDto?> GetDetailAsync(Guid userId, Guid blueprintId, CancellationToken ct = default) =>
            throw new NotImplementedException();
    }

    [Fact]
    public async Task Handle_OrgHasNoBlueprints_ReturnsEmptyList()
    {
        var repo = new FakeRepo();
        var handler = new GetOrgBlueprintsHandler(repo);

        var result = await handler.HandleAsync(new GetOrgBlueprintsQuery(UserId));

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_OrgHasBlueprints_ReturnsDeduplicatedList()
    {
        var blueprintId = Guid.NewGuid();
        var repo = new FakeRepo();
        repo.SeedList([new OrgBlueprintListItemDto(blueprintId, "Widget Mk1", "Weapon", null, null, 3)]);
        var handler = new GetOrgBlueprintsHandler(repo);

        var result = await handler.HandleAsync(new GetOrgBlueprintsQuery(UserId));

        result.Should().HaveCount(1);
        result[0].BlueprintId.Should().Be(blueprintId);
        result[0].ProductName.Should().Be("Widget Mk1");
        result[0].Type.Should().Be("Weapon");
        result[0].IngredientCount.Should().Be(3);
    }

    [Fact]
    public async Task Handle_PassesUserIdToRepository()
    {
        Guid? capturedUserId = null;
        var repo = new CapturingFakeRepo(id => capturedUserId = id);
        var handler = new GetOrgBlueprintsHandler(repo);

        await handler.HandleAsync(new GetOrgBlueprintsQuery(UserId));

        capturedUserId.Should().Be(UserId);
    }

    private sealed class CapturingFakeRepo(Action<Guid> onCall) : IOrgBlueprintRepository
    {
        public Task<IReadOnlyList<OrgBlueprintListItemDto>> GetListAsync(Guid userId, CancellationToken ct = default)
        {
            onCall(userId);
            return Task.FromResult<IReadOnlyList<OrgBlueprintListItemDto>>([]);
        }

        public Task<OrgBlueprintDetailDto?> GetDetailAsync(Guid userId, Guid blueprintId, CancellationToken ct = default) =>
            throw new NotImplementedException();
    }
}
