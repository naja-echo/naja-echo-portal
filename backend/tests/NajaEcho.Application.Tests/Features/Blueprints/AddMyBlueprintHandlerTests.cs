using FluentAssertions;
using NajaEcho.Application.Abstractions;
using NajaEcho.Application.Features.Blueprints.AddMyBlueprint;
using NajaEcho.Application.Features.Blueprints.GetBlueprintDetail;
using NajaEcho.Application.Features.Blueprints.GetMyBlueprints;
using NajaEcho.Domain.Blueprints;
using Xunit;

namespace NajaEcho.Application.Tests.Features.Blueprints;

public class AddMyBlueprintHandlerTests
{
    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");

    private sealed class FakeUserBlueprintRepo : IUserBlueprintRepository
    {
        private readonly HashSet<(Guid UserId, Guid BlueprintId)> _existing = [];
        private readonly HashSet<Guid> _catalogIds;

        public FakeUserBlueprintRepo(IEnumerable<Guid>? catalogIds = null)
        {
            _catalogIds = catalogIds?.ToHashSet() ?? [];
        }

        public Task<IReadOnlyList<MyBlueprintListItemDto>> GetListAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<MyBlueprintListItemDto>>([]);

        public Task<MyBlueprintListItemDto> AddAsync(Guid userId, Guid blueprintId, CancellationToken ct = default)
        {
            if (!_catalogIds.Contains(blueprintId))
                throw new BlueprintNotFoundException(blueprintId);

            if (!_existing.Add((userId, blueprintId)))
                throw new DuplicateBlueprintException(blueprintId);

            return Task.FromResult(new MyBlueprintListItemDto(blueprintId, "Widget", "Weapon", null, 2));
        }

        public Task<BlueprintDetailDto?> GetDetailAsync(Guid userId, Guid blueprintId, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task<bool> RemoveAsync(Guid userId, Guid blueprintId, CancellationToken ct = default) =>
            throw new NotImplementedException();
    }

    [Fact]
    public async Task Handle_ValidBlueprint_ReturnsDto()
    {
        var blueprintId = Guid.NewGuid();
        var repo = new FakeUserBlueprintRepo([blueprintId]);
        var handler = new AddMyBlueprintHandler(repo);

        var result = await handler.HandleAsync(new AddMyBlueprintCommand(UserId, blueprintId));

        result.Should().NotBeNull();
        result.BlueprintId.Should().Be(blueprintId);
        result.ProductName.Should().Be("Widget");
        result.IngredientCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_DuplicateBlueprint_ThrowsDuplicateBlueprintException()
    {
        var blueprintId = Guid.NewGuid();
        var repo = new FakeUserBlueprintRepo([blueprintId]);
        var handler = new AddMyBlueprintHandler(repo);

        await handler.HandleAsync(new AddMyBlueprintCommand(UserId, blueprintId));

        var act = async () => await handler.HandleAsync(new AddMyBlueprintCommand(UserId, blueprintId));
        await act.Should().ThrowAsync<DuplicateBlueprintException>();
    }

    [Fact]
    public async Task Handle_UnknownBlueprint_ThrowsBlueprintNotFoundException()
    {
        var repo = new FakeUserBlueprintRepo();
        var handler = new AddMyBlueprintHandler(repo);

        var act = async () => await handler.HandleAsync(new AddMyBlueprintCommand(UserId, Guid.NewGuid()));
        await act.Should().ThrowAsync<BlueprintNotFoundException>();
    }
}
