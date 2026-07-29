using FluentAssertions;
using NajaEcho.Application.Abstractions;
using NajaEcho.Application.Features.Blueprints.GetBlueprintDetail;
using NajaEcho.Application.Features.Blueprints.GetOrgBlueprintDetail;
using NajaEcho.Application.Features.Blueprints.GetOrgBlueprints;
using Xunit;

namespace NajaEcho.Application.Tests.Features.Blueprints;

public class GetOrgBlueprintDetailHandlerTests
{
    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid BlueprintId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");

    private sealed class FakeRepo : IOrgBlueprintRepository
    {
        private IReadOnlyList<OrgBlueprintListItemDto> _list = [];
        private OrgBlueprintDetailDto? _detail;

        public void SeedDetail(OrgBlueprintDetailDto? detail) => _detail = detail;

        public Task<IReadOnlyList<OrgBlueprintListItemDto>> GetListAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult(_list);

        public Task<OrgBlueprintDetailDto?> GetDetailAsync(Guid userId, Guid blueprintId, CancellationToken ct = default) =>
            Task.FromResult(_detail);
    }

    [Fact]
    public async Task Handle_NoBlueprintInOrg_ReturnsNull()
    {
        var repo = new FakeRepo();
        var handler = new GetOrgBlueprintDetailHandler(repo);

        var result = await handler.HandleAsync(new GetOrgBlueprintDetailQuery(UserId, BlueprintId));

        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_BlueprintInOrg_ReturnsCorrectDetail()
    {
        var repo = new FakeRepo();
        var detail = new OrgBlueprintDetailDto(
            BlueprintId,
            "Widget Mk1",
            "Weapon",
            330,
            2,
            [new BlueprintSlotDto(0, "Barrel", [new BlueprintSlotOptionDto(0, "Steel", "Material", 1.5m)])],
            [new OrgBlueprintOwnerDto(UserId, "TestUser")]);
        repo.SeedDetail(detail);
        var handler = new GetOrgBlueprintDetailHandler(repo);

        var result = await handler.HandleAsync(new GetOrgBlueprintDetailQuery(UserId, BlueprintId));

        result.Should().NotBeNull();
        result!.BlueprintId.Should().Be(BlueprintId);
        result.ProductName.Should().Be("Widget Mk1");
        result.Type.Should().Be("Weapon");
        result.CraftTimeSeconds.Should().Be(330);
        result.IngredientCount.Should().Be(2);
        result.Slots.Should().HaveCount(1);
        result.Owners.Should().HaveCount(1);
        result.Owners[0].DisplayName.Should().Be("TestUser");
    }

    [Fact]
    public async Task Handle_BlueprintWithNoSlots_ReturnsEmptySlotsList()
    {
        var repo = new FakeRepo();
        repo.SeedDetail(new OrgBlueprintDetailDto(BlueprintId, "Widget", null, null, 0, [], []));
        var handler = new GetOrgBlueprintDetailHandler(repo);

        var result = await handler.HandleAsync(new GetOrgBlueprintDetailQuery(UserId, BlueprintId));

        result!.Slots.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_BlueprintWithMultipleOwners_ReturnsAllOwners()
    {
        var userId2 = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");
        var repo = new FakeRepo();
        repo.SeedDetail(new OrgBlueprintDetailDto(
            BlueprintId, "Widget", null, null, 0, [],
            [
                new OrgBlueprintOwnerDto(UserId, "Alice"),
                new OrgBlueprintOwnerDto(userId2, "Bob"),
            ]));
        var handler = new GetOrgBlueprintDetailHandler(repo);

        var result = await handler.HandleAsync(new GetOrgBlueprintDetailQuery(UserId, BlueprintId));

        result!.Owners.Should().HaveCount(2);
        result.Owners.Select(o => o.DisplayName).Should().BeEquivalentTo(["Alice", "Bob"]);
    }
}
