using FluentAssertions;
using NajaEcho.Application.Abstractions;
using NajaEcho.Application.Features.Blueprints.GetBlueprintDetail;
using NajaEcho.Application.Features.Blueprints.GetMyBlueprints;
using Xunit;

namespace NajaEcho.Application.Tests.Features.Blueprints;

public class GetMyBlueprintsHandlerTests
{
    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid OtherUserId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");

    private sealed class FakeUserBlueprintRepository : IUserBlueprintRepository
    {
        private readonly Dictionary<Guid, List<MyBlueprintListItemDto>> _data = new();

        public void Seed(Guid userId, IEnumerable<MyBlueprintListItemDto> items)
        {
            _data[userId] = items.ToList();
        }

        public Task<IReadOnlyList<MyBlueprintListItemDto>> GetListAsync(Guid userId, CancellationToken ct = default)
        {
            var result = _data.TryGetValue(userId, out var list)
                ? (IReadOnlyList<MyBlueprintListItemDto>)list
                : [];
            return Task.FromResult(result);
        }

        public Task<MyBlueprintListItemDto> AddAsync(Guid userId, Guid blueprintId, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task<BlueprintDetailDto?> GetDetailAsync(Guid userId, Guid blueprintId, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task<bool> RemoveAsync(Guid userId, Guid blueprintId, CancellationToken ct = default) =>
            throw new NotImplementedException();
    }

    [Fact]
    public async Task Handle_UserHasNoBlueprints_ReturnsEmptyList()
    {
        var repo = new FakeUserBlueprintRepository();
        var handler = new GetMyBlueprintsHandler(repo);

        var result = await handler.HandleAsync(new GetMyBlueprintsQuery(UserId));

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_UserHasBlueprints_ReturnsCorrectFields()
    {
        var blueprintId = Guid.NewGuid();
        var repo = new FakeUserBlueprintRepository();
        repo.Seed(UserId, [new MyBlueprintListItemDto(blueprintId, "Widget Mk1", "Weapon", 3)]);
        var handler = new GetMyBlueprintsHandler(repo);

        var result = await handler.HandleAsync(new GetMyBlueprintsQuery(UserId));

        result.Should().HaveCount(1);
        result[0].BlueprintId.Should().Be(blueprintId);
        result[0].ProductName.Should().Be("Widget Mk1");
        result[0].Type.Should().Be("Weapon");
        result[0].IngredientCount.Should().Be(3);
    }

    [Fact]
    public async Task Handle_OnlyReturnsCallerBlueprints_NotOtherUsers()
    {
        var repo = new FakeUserBlueprintRepository();
        repo.Seed(OtherUserId, [new MyBlueprintListItemDto(Guid.NewGuid(), "Other Widget", null, 1)]);
        var handler = new GetMyBlueprintsHandler(repo);

        var result = await handler.HandleAsync(new GetMyBlueprintsQuery(UserId));

        result.Should().BeEmpty();
    }
}
