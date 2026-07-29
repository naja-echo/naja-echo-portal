using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NajaEcho.Application.Features.Characters.VerifyCharacter;
using NajaEcho.Domain.Characters;
using NajaEcho.Domain.Users;
using NajaEcho.Infrastructure.Characters;
using NajaEcho.Infrastructure.Identity;
using NajaEcho.Infrastructure.Persistence;
using Xunit;

namespace NajaEcho.Infrastructure.Tests.Identity;

[Collection(PostgresCollection.Name)]
public sealed class UserRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    private readonly List<IServiceScope> _scopes = [];
    private ServiceProvider _serviceProvider = null!;
    private AppDbContext _db = null!;

    public UserRepositoryIntegrationTests(PostgresFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        await _fixture.ResetAsync();
        _serviceProvider = _fixture.BuildIdentityProvider();
        _db = _fixture.CreateContext();
    }

    public async Task DisposeAsync()
    {
        foreach (var scope in _scopes)
        {
            scope.Dispose();
        }

        _scopes.Clear();
        await _serviceProvider.DisposeAsync();
        await _db.DisposeAsync();
    }

    private ApplicationUser AddUser(string name = "Test")
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            DisplayName = name,
            DiscordUsername = name.ToLower(),
            UserName = name,
            NormalizedUserName = name.ToUpper(),
            SecurityStamp = Guid.NewGuid().ToString(),
        };
        _db.Set<ApplicationUser>().Add(user);
        return user;
    }

    private async Task<IdentityRole<Guid>> AddRoleAsync(string roleName)
    {
        var role = new IdentityRole<Guid>
        {
            Id = Guid.NewGuid(),
            Name = roleName,
            NormalizedName = roleName.ToUpper(),
            ConcurrencyStamp = Guid.NewGuid().ToString(),
        };
        _db.Set<IdentityRole<Guid>>().Add(role);
        await _db.SaveChangesAsync();
        return role;
    }

    private async Task AssignRoleAsync(Guid userId, Guid roleId)
    {
        _db.Set<IdentityUserRole<Guid>>().Add(new IdentityUserRole<Guid>
        {
            UserId = userId,
            RoleId = roleId,
        });
        await _db.SaveChangesAsync();
    }

    // The scope must outlive the returned repository — UserManager is scoped, and disposing the
    // scope here would leave the repository holding a disposed UserManager.
    private UserRepository MakeUserRepo()
    {
        var scope = _serviceProvider.CreateScope();
        _scopes.Add(scope);
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        return new UserRepository(_db, userManager);
    }

    private CharacterRepository MakeCharRepo() => new(_db);

    // ── US1: GetUsersWithRolesAndCharactersAsync ───────────────────────────

    [Fact]
    public async Task GetUsersWithRolesAndCharacters_ReturnsAllMembers()
    {
        var user1 = AddUser("Alice");
        var user2 = AddUser("Bob");
        await _db.SaveChangesAsync();

        var repo = MakeUserRepo();
        var result = await repo.GetUsersWithRolesAndCharactersAsync(CancellationToken.None);

        result.Should().HaveCount(2);
        result.Select(u => u.AuthName).Should().Contain(["Alice", "Bob"]);
    }

    [Fact]
    public async Task GetUsersWithRolesAndCharacters_MemberWithNoRolesOrCharacters_YieldsEmptyArrays()
    {
        var user = AddUser("Lonely");
        await _db.SaveChangesAsync();

        var repo = MakeUserRepo();
        var result = await repo.GetUsersWithRolesAndCharactersAsync(CancellationToken.None);

        var dto = result.Single(u => u.Id == user.Id);
        dto.Roles.Should().BeEmpty();
        dto.Characters.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUsersWithRolesAndCharacters_GroupsRolesCorrectly()
    {
        var user = AddUser("MultiRole");
        await _db.SaveChangesAsync();
        var adminRole = await AddRoleAsync("Admin");
        var qmRole = await AddRoleAsync("Quartermaster");
        await AssignRoleAsync(user.Id, adminRole.Id);
        await AssignRoleAsync(user.Id, qmRole.Id);

        var repo = MakeUserRepo();
        var result = await repo.GetUsersWithRolesAndCharactersAsync(CancellationToken.None);

        var dto = result.Single(u => u.Id == user.Id);
        dto.Roles.Should().HaveCount(2);
        dto.Roles.Should().Contain("Admin");
        dto.Roles.Should().Contain("Quartermaster");
    }

    [Fact]
    public async Task GetUsersWithRolesAndCharacters_GroupsCharactersCorrectly()
    {
        var user = AddUser("CharUser");
        await _db.SaveChangesAsync();

        var charRepo = MakeCharRepo();
        var c1 = new Character { Id = Guid.NewGuid(), OwnerUserId = user.Id, Name = "CharOne", Handle = "charone", CreatedAt = DateTimeOffset.UtcNow };
        var c2 = new Character { Id = Guid.NewGuid(), OwnerUserId = user.Id, Name = "CharTwo", Handle = "chartwo", CreatedAt = DateTimeOffset.UtcNow };
        await charRepo.AddAsync(c1, CancellationToken.None);
        await charRepo.AddAsync(c2, CancellationToken.None);

        var repo = MakeUserRepo();
        var result = await repo.GetUsersWithRolesAndCharactersAsync(CancellationToken.None);

        var dto = result.Single(u => u.Id == user.Id);
        dto.Characters.Should().HaveCount(2);
        dto.Characters.Select(c => c.Handle).Should().Contain(["charone", "chartwo"]);
    }

    // ── GetRolesAsync: the read behind cookie role refresh ───────────────────

    [Fact]
    public async Task GetRoles_ReturnsEveryRoleTheUserHolds()
    {
        var user = AddUser("Holder");
        await _db.SaveChangesAsync();
        var adminRole = await AddRoleAsync(Roles.Admin);
        var qmRole = await AddRoleAsync(Roles.Quartermaster);
        await AssignRoleAsync(user.Id, adminRole.Id);
        await AssignRoleAsync(user.Id, qmRole.Id);

        var repo = MakeUserRepo();
        var roles = await repo.GetRolesAsync(user.Id, CancellationToken.None);

        roles.Should().BeEquivalentTo([Roles.Admin, Roles.Quartermaster]);
    }

    [Fact]
    public async Task GetRoles_UserWithNoRoles_ReturnsEmpty()
    {
        var user = AddUser("Roleless");
        await _db.SaveChangesAsync();

        var repo = MakeUserRepo();
        var roles = await repo.GetRolesAsync(user.Id, CancellationToken.None);

        roles.Should().BeEmpty();
    }

    [Fact]
    public async Task GetRoles_UnknownUser_ReturnsEmptyRatherThanThrowing()
    {
        var repo = MakeUserRepo();

        var roles = await repo.GetRolesAsync(Guid.NewGuid(), CancellationToken.None);

        roles.Should().BeEmpty();
    }

    [Fact]
    public async Task SetRoles_ReplacesThePreviousSet()
    {
        var user = AddUser("Switcher");
        await _db.SaveChangesAsync();
        var qmRole = await AddRoleAsync(Roles.Quartermaster);
        await AddRoleAsync(Roles.CrewResourceOfficer);
        await AssignRoleAsync(user.Id, qmRole.Id);

        var repo = MakeUserRepo();
        await repo.SetRolesAsync(user.Id, [Roles.CrewResourceOfficer], CancellationToken.None);

        var roles = await repo.GetRolesAsync(user.Id, CancellationToken.None);
        roles.Should().BeEquivalentTo([Roles.CrewResourceOfficer]);
    }

    // ── US2: Admin insert honours unique handle index ────────────────────────

    [Fact]
    public async Task AdminAddCharacter_PersistsWithCorrectOwner()
    {
        var user = AddUser("TargetUser");
        await _db.SaveChangesAsync();

        var charRepo = MakeCharRepo();
        var character = new Character
        {
            Id = Guid.NewGuid(),
            OwnerUserId = user.Id,
            Name = "Admin Added",
            Handle = "adminhandle",
            CreatedAt = DateTimeOffset.UtcNow,
        };
        await charRepo.AddAsync(character, CancellationToken.None);

        var stored = await _db.Characters.FirstOrDefaultAsync(c => c.Id == character.Id);
        stored.Should().NotBeNull();
        stored!.OwnerUserId.Should().Be(user.Id);
    }

    [Fact]
    public async Task AdminAddCharacter_DuplicateHandle_ThrowsHandleAlreadyClaimedException()
    {
        var user1 = AddUser("Owner1");
        var user2 = AddUser("Owner2");
        await _db.SaveChangesAsync();

        var charRepo = MakeCharRepo();
        var c1 = new Character { Id = Guid.NewGuid(), OwnerUserId = user1.Id, Name = "Alpha", Handle = "SharedHandle", CreatedAt = DateTimeOffset.UtcNow };
        await charRepo.AddAsync(c1, CancellationToken.None);

        var c2 = new Character { Id = Guid.NewGuid(), OwnerUserId = user2.Id, Name = "Beta", Handle = "sharedhandle", CreatedAt = DateTimeOffset.UtcNow };
        var act = () => charRepo.AddAsync(c2, CancellationToken.None);
        await act.Should().ThrowAsync<HandleAlreadyClaimedException>();
    }
}
