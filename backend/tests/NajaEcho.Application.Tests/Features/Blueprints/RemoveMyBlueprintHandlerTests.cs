using FluentAssertions;
using NajaEcho.Application.Abstractions;
using NajaEcho.Application.Features.Blueprints.GetBlueprintDetail;
using NajaEcho.Application.Features.Blueprints.GetMyBlueprints;
using NajaEcho.Application.Features.Blueprints.RemoveMyBlueprint;
using Xunit;

namespace NajaEcho.Application.Tests.Features.Blueprints;

public class RemoveMyBlueprintHandlerTests
{
    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

    private sealed class FakeRepo : IUserBlueprintRepository
    {
        private readonly HashSet<(Guid, Guid)> _owned = [];

        public void Seed(Guid userId, Guid blueprintId) =>
            _owned.Add((userId, blueprintId));

        public Task<bool> RemoveAsync(Guid userId, Guid blueprintId, CancellationToken ct = default)
        {
            var removed = _owned.Remove((userId, blueprintId));
            return Task.FromResult(removed);
        }

        public Task<IReadOnlyList<MyBlueprintListItemDto>> GetListAsync(Guid userId, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task<MyBlueprintListItemDto> AddAsync(Guid userId, Guid blueprintId, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task<BlueprintDetailDto?> GetDetailAsync(Guid userId, Guid blueprintId, CancellationToken ct = default) =>
            throw new NotImplementedException();
    }

    [Fact]
    public async Task Handle_BlueprintInUserList_ReturnsTrue()
    {
        var blueprintId = Guid.NewGuid();
        var repo = new FakeRepo();
        repo.Seed(UserId, blueprintId);
        var handler = new RemoveMyBlueprintHandler(repo);

        var result = await handler.HandleAsync(new RemoveMyBlueprintCommand(UserId, blueprintId));

        result.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_BlueprintNotInUserList_ReturnsFalse()
    {
        var repo = new FakeRepo();
        var handler = new RemoveMyBlueprintHandler(repo);

        var result = await handler.HandleAsync(new RemoveMyBlueprintCommand(UserId, Guid.NewGuid()));

        result.Should().BeFalse();
    }
}
