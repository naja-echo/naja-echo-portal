using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NajaEcho.Application.Abstractions;
using NajaEcho.Application.Features.Admin.Users.AddCharacterForUser;
using NajaEcho.Application.Features.Admin.Users.AssignRoles;
using NajaEcho.Application.Features.Admin.Users.GetUsers;
using NajaEcho.Domain.Organizations;

namespace NajaEcho.Application.Tests.Features.Admin.Users;

public sealed class AssignRolesHandlerTests
{
    private sealed class FakeUserRepo : IUserRepository
    {
        public bool Exists { get; set; } = true;
        public IReadOnlyList<string>? LastRolesSet { get; private set; }

        public Task<bool> ExistsAsync(Guid userId, CancellationToken ct) => Task.FromResult(Exists);
        public Task<IReadOnlyList<(Guid Id, string DisplayName)>> GetAllAsync(CancellationToken ct)
            => Task.FromResult<IReadOnlyList<(Guid, string)>>([]);
        public Task<IReadOnlyList<AdminUserDto>> GetUsersWithRolesAndCharactersAsync(CancellationToken ct)
            => Task.FromResult<IReadOnlyList<AdminUserDto>>([]);
        public Task SetRolesAsync(Guid userId, IReadOnlyList<string> roles, CancellationToken ct)
        {
            LastRolesSet = roles;
            return Task.CompletedTask;
        }


        public Task<IReadOnlyList<string>> GetRolesAsync(Guid userId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<string>>(LastRolesSet ?? []);
    }

    private sealed class FakeSessionInvalidator : IUserSessionInvalidator
    {
        public List<Guid> Invalidated { get; } = [];

        public void Invalidate(Guid userId) => Invalidated.Add(userId);

        public bool IsStaleSince(Guid userId, DateTimeOffset refreshedAt) => false;
    }

    private static (AssignRolesHandler handler, FakeUserRepo repo, FakeSessionInvalidator invalidator) MakeHandler()
    {
        var repo = new FakeUserRepo();
        var invalidator = new FakeSessionInvalidator();
        var handler = new AssignRolesHandler(repo, invalidator, NullLogger<AssignRolesHandler>.Instance);
        return (handler, repo, invalidator);
    }

    [Fact]
    public async Task HandleAsync_ValidRoles_CallsSetRolesAsync()
    {
        var (handler, repo, _) = MakeHandler();
        var userId = Guid.NewGuid();

        await handler.HandleAsync(new AssignRolesCommand(userId, ["Admin", "Quartermaster"]), default);

        repo.LastRolesSet.Should().BeEquivalentTo(["Admin", "Quartermaster"]);
    }

    [Fact]
    public async Task HandleAsync_EmptyRoles_CallsSetRolesAsyncWithEmpty()
    {
        var (handler, repo, _) = MakeHandler();
        var userId = Guid.NewGuid();

        await handler.HandleAsync(new AssignRolesCommand(userId, []), default);

        repo.LastRolesSet.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_UserNotFound_ThrowsUserNotFoundException()
    {
        var (handler, repo, _) = MakeHandler();
        repo.Exists = false;
        var userId = Guid.NewGuid();

        var act = async () => await handler.HandleAsync(new AssignRolesCommand(userId, ["Admin"]), default);

        await act.Should().ThrowAsync<UserNotFoundException>();
    }

    [Fact]
    public async Task HandleAsync_InvalidRole_ThrowsInvalidRoleException()
    {
        var (handler, _, _) = MakeHandler();
        var userId = Guid.NewGuid();

        var act = async () => await handler.HandleAsync(new AssignRolesCommand(userId, ["NotARealRole"]), default);

        await act.Should().ThrowAsync<InvalidRoleException>();
    }

    [Fact]
    public async Task HandleAsync_InvalidRoleAmongValidOnes_ThrowsInvalidRoleException()
    {
        var (handler, _, _) = MakeHandler();
        var userId = Guid.NewGuid();

        var act = async () => await handler.HandleAsync(
            new AssignRolesCommand(userId, ["Admin", "Hacker"]), default);

        await act.Should().ThrowAsync<InvalidRoleException>();
    }

    [Fact]
    public async Task HandleAsync_Success_InvalidatesTheTargetUsersSession()
    {
        var (handler, _, invalidator) = MakeHandler();
        var userId = Guid.NewGuid();

        await handler.HandleAsync(new AssignRolesCommand(userId, ["Quartermaster"]), default);

        invalidator.Invalidated.Should().Equal(userId);
    }

    [Fact]
    public async Task HandleAsync_RevokingEveryRole_StillInvalidatesTheSession()
    {
        var (handler, _, invalidator) = MakeHandler();
        var userId = Guid.NewGuid();

        await handler.HandleAsync(new AssignRolesCommand(userId, []), default);

        invalidator.Invalidated.Should().Equal(userId);
    }

    [Fact]
    public async Task HandleAsync_InvalidRole_DoesNotInvalidateAnySession()
    {
        var (handler, _, invalidator) = MakeHandler();

        var act = async () => await handler.HandleAsync(
            new AssignRolesCommand(Guid.NewGuid(), ["NotARealRole"]), default);

        await act.Should().ThrowAsync<InvalidRoleException>();
        invalidator.Invalidated.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_UserNotFound_DoesNotInvalidateAnySession()
    {
        var (handler, repo, invalidator) = MakeHandler();
        repo.Exists = false;

        var act = async () => await handler.HandleAsync(
            new AssignRolesCommand(Guid.NewGuid(), ["Admin"]), default);

        await act.Should().ThrowAsync<UserNotFoundException>();
        invalidator.Invalidated.Should().BeEmpty();
    }
}
