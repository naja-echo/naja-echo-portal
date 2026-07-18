using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NajaEcho.Application.Abstractions;
using NajaEcho.Domain.Users;
using NajaEcho.Infrastructure.Identity;
using NajaEcho.Infrastructure.Persistence;
using Xunit;

namespace NajaEcho.Infrastructure.Tests.Identity;

[Collection(PostgresCollection.Name)]
public class DiscordExternalLoginServiceTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    private ServiceProvider _serviceProvider = null!;

    public DiscordExternalLoginServiceTests(PostgresFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        await _fixture.ResetAsync();
        _serviceProvider = _fixture.BuildIdentityProvider(services =>
            services.AddScoped<IExternalLoginService, DiscordExternalLoginService>());
    }

    public async Task DisposeAsync() => await _serviceProvider.DisposeAsync();

    private IExternalLoginService CreateService()
    {
        // Use a new scope per test so UserManager state is isolated
        var scope = _serviceProvider.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IExternalLoginService>();
    }

    private UserManager<ApplicationUser> GetUserManager()
    {
        var scope = _serviceProvider.CreateScope();
        return scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    }

    private static DiscordProfile MakeProfile(
        string id = "123456789012345678",
        string username = "testuser",
        string? globalName = "Test User") =>
        new() { Id = id, Username = username, GlobalName = globalName };

    // T014: First-time login creates user and links Discord provider
    [Fact]
    public async Task FindOrCreateAsync_FirstTime_CreatesUserAndLinksDiscordLogin()
    {
        var service = CreateService();
        var profile = MakeProfile();

        var result = await service.FindOrCreateAsync(profile);

        result.Id.Should().NotBe(Guid.Empty);
        result.DisplayName.Should().Be("Test User");
        result.DiscordUsername.Should().Be("testuser");

        var user = await GetUserManager().FindByLoginAsync("Discord", "123456789012345678");
        user.Should().NotBeNull();
        user!.Id.Should().Be(result.Id);
    }

    // T022: Returning login reuses existing user (no duplicate created)
    [Fact]
    public async Task FindOrCreateAsync_ReturningUser_ReusesExistingUserNoDuplicate()
    {
        var service = CreateService();
        var profile = MakeProfile();

        var first = await service.FindOrCreateAsync(profile);
        var second = await service.FindOrCreateAsync(profile);

        second.Id.Should().Be(first.Id);

        using var scope = _serviceProvider.CreateScope();
        var users = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.ToListAsync();
        users.Should().HaveCount(1);
    }

    // T023: Returning login updates display name when changed at Discord
    [Fact]
    public async Task FindOrCreateAsync_UpdatesDisplayName_WhenChangedAtDiscord()
    {
        var service = CreateService();
        var original = MakeProfile(globalName: "Old Name");
        await service.FindOrCreateAsync(original);

        var updated = MakeProfile(globalName: "New Name");
        var result = await service.FindOrCreateAsync(updated);

        result.DisplayName.Should().Be("New Name");

        var user = await GetUserManager().FindByLoginAsync("Discord", original.Id);
        user!.DisplayName.Should().Be("New Name");
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenUserNotFound()
    {
        var service = CreateService();

        var result = await service.GetByIdAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsLocalUser_WhenUserExists()
    {
        var service = CreateService();
        var created = await service.FindOrCreateAsync(MakeProfile());

        var result = await service.GetByIdAsync(created.Id);

        result.Should().NotBeNull();
        result!.Id.Should().Be(created.Id);
        result.DisplayName.Should().Be("Test User");
    }
}
