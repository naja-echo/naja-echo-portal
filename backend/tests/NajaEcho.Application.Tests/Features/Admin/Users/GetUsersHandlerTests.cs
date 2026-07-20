using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NajaEcho.Application.Abstractions;
using NajaEcho.Application.Features.Admin.Users.GetUsers;
using NajaEcho.Domain.Organizations;

namespace NajaEcho.Application.Tests.Features.Admin.Users;

public sealed class GetUsersHandlerTests
{
    private sealed class FakeUserRepo(IReadOnlyList<AdminUserDto> users) : IUserRepository
    {
        public Task<bool> ExistsAsync(Guid userId, CancellationToken ct) => Task.FromResult(false);
        public Task<IReadOnlyList<(Guid Id, string DisplayName)>> GetAllAsync(CancellationToken ct)
            => Task.FromResult<IReadOnlyList<(Guid, string)>>([]);
        public Task<IReadOnlyList<AdminUserDto>> GetUsersWithRolesAndCharactersAsync(CancellationToken ct)
            => Task.FromResult(users);
        public Task SetRolesAsync(Guid userId, IReadOnlyList<string> roles, CancellationToken ct) => Task.CompletedTask;


        public Task<IReadOnlyList<string>> GetRolesAsync(Guid userId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<string>>([]);
    }

    private static GetUsersHandler MakeHandler(IReadOnlyList<AdminUserDto> users) =>
        new(new FakeUserRepo(users), NullLogger<GetUsersHandler>.Instance);

    [Fact]
    public async Task HandleAsync_ReturnsAllMembers()
    {
        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();
        var users = new List<AdminUserDto>
        {
            new(userId1, "alice", ["Admin"], [new(Guid.NewGuid(), "Alice Char", "alicehandle")], null),
            new(userId2, "bob", [], [], null),
        };
        var handler = MakeHandler(users);

        var result = await handler.HandleAsync(new GetUsersQuery(), default);

        result.Should().HaveCount(2);
        result[0].Id.Should().Be(userId1);
        result[1].Id.Should().Be(userId2);
    }

    [Fact]
    public async Task HandleAsync_MemberWithNoRolesOrCharacters_YieldsEmptyArrays()
    {
        var userId = Guid.NewGuid();
        var users = new List<AdminUserDto>
        {
            new(userId, "emptyuser", [], [], null),
        };
        var handler = MakeHandler(users);

        var result = await handler.HandleAsync(new GetUsersQuery(), default);

        result.Should().HaveCount(1);
        result[0].Roles.Should().BeEmpty();
        result[0].Characters.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_GroupsRolesAndCharactersCorrectlyPerMember()
    {
        var userId = Guid.NewGuid();
        var charId1 = Guid.NewGuid();
        var charId2 = Guid.NewGuid();
        var users = new List<AdminUserDto>
        {
            new(userId, "multiuser",
                ["Admin", "Quartermaster"],
                [
                    new(charId1, "Char One", "handle1"),
                    new(charId2, "Char Two", "handle2"),
                ],
                null),
        };
        var handler = MakeHandler(users);

        var result = await handler.HandleAsync(new GetUsersQuery(), default);

        result[0].Roles.Should().ContainInOrder("Admin", "Quartermaster");
        result[0].Characters.Should().HaveCount(2);
        result[0].Characters[0].Id.Should().Be(charId1);
        result[0].Characters[1].Handle.Should().Be("handle2");
    }

    [Fact]
    public async Task HandleAsync_EmptyRoster_ReturnsEmptyList()
    {
        var handler = MakeHandler([]);

        var result = await handler.HandleAsync(new GetUsersQuery(), default);

        result.Should().BeEmpty();
    }
}
