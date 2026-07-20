using FluentAssertions;
using Microsoft.Extensions.Logging;
using NajaEcho.Application.Abstractions;
using NajaEcho.Application.Features.Admin.Organizations.AssignOrganization;
using NajaEcho.Application.Features.Admin.Users.AddCharacterForUser;
using NajaEcho.Application.Features.Admin.Users.GetUsers;
using NajaEcho.Domain.Organizations;

namespace NajaEcho.Application.Tests.Features.Admin.Organizations;

public sealed class AssignOrganizationHandlerTests
{
    private static readonly Guid CallerId = Guid.Parse("11111111-0000-4000-a000-000000000001");
    private static readonly Guid TargetUserId = Guid.Parse("22222222-0000-4000-a000-000000000002");
    private static readonly Guid OrgAId = Guid.Parse("aaaaaaaa-0000-4000-a000-00000000000a");
    private static readonly Guid OrgBId = Guid.Parse("bbbbbbbb-0000-4000-a000-00000000000b");

    // ── Validation ────────────────────────────────────────────────────────

    [Fact]
    public async Task UnknownUser_ThrowsUserNotFound()
    {
        var (handler, userRepo, _, _, _) = Make();
        userRepo.Exists = false;

        var act = async () => await handler.HandleAsync(
            new AssignOrganizationCommand(TargetUserId, OrgAId, CallerId), CancellationToken.None);

        await act.Should().ThrowAsync<UserNotFoundException>();
    }

    [Fact]
    public async Task UnknownOrganization_ThrowsOrganizationNotFound()
    {
        var (handler, _, orgRepo, _, _) = Make();
        orgRepo.Exists = false;

        var act = async () => await handler.HandleAsync(
            new AssignOrganizationCommand(TargetUserId, OrgAId, CallerId), CancellationToken.None);

        await act.Should().ThrowAsync<OrganizationNotFoundException>();
    }

    [Fact]
    public async Task ClearingDoesNotRequireAnExistingOrganization()
    {
        var (handler, _, orgRepo, _, _) = Make();
        orgRepo.Exists = false;

        var act = async () => await handler.HandleAsync(
            new AssignOrganizationCommand(TargetUserId, null, CallerId), CancellationToken.None);

        await act.Should().NotThrowAsync(
            "there is no organization to look up when clearing — requiring one would make an " +
            "unassignment impossible whenever the member's organization had been removed");
    }

    // ── Behaviour ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Success_WritesTheAssignmentAndInvalidatesTheTargetsSession()
    {
        var (handler, _, orgRepo, invalidator, _) = Make();

        await handler.HandleAsync(
            new AssignOrganizationCommand(TargetUserId, OrgBId, CallerId), CancellationToken.None);

        orgRepo.LastSet.Should().NotBeNull();
        orgRepo.LastSet!.Value.UserId.Should().Be(TargetUserId);
        orgRepo.LastSet.Value.OrganizationId.Should().Be(OrgBId);
        invalidator.Invalidated.Should().ContainSingle().Which.Should().Be(TargetUserId,
            "the member's live session holds their organization as a claim; without this the " +
            "change would not reach them until the session expired");
    }

    [Fact]
    public async Task Success_InvalidatesTheTargetNotTheActingAdmin()
    {
        var (handler, _, _, invalidator, _) = Make();

        await handler.HandleAsync(
            new AssignOrganizationCommand(TargetUserId, OrgBId, CallerId), CancellationToken.None);

        invalidator.Invalidated.Should().NotContain(CallerId);
    }

    [Fact]
    public async Task AssigningTheOrganizationAlreadyCurrent_StillDelegatesToTheRepository()
    {
        var (handler, _, orgRepo, _, _) = Make();
        orgRepo.Current = new Organization { Id = OrgAId, Name = "Org A" };

        await handler.HandleAsync(
            new AssignOrganizationCommand(TargetUserId, OrgAId, CallerId), CancellationToken.None);

        // The no-op decision belongs to the repository, which can see the stored state in one
        // place. Duplicating it here would mean two implementations of "already current".
        orgRepo.LastSet.Should().NotBeNull();
        orgRepo.LastSet!.Value.OrganizationId.Should().Be(OrgAId);
    }

    // ── FR-018 / FR-019: the log event ────────────────────────────────────

    [Fact]
    public async Task LogEvent_CarriesActingAdminTargetPreviousAndNewOrganization()
    {
        var (handler, _, orgRepo, _, log) = Make();
        orgRepo.Current = new Organization { Id = OrgAId, Name = "Org A" };

        await handler.HandleAsync(
            new AssignOrganizationCommand(TargetUserId, OrgBId, CallerId), CancellationToken.None);

        var entry = log.Entries.Should().ContainSingle().Subject;

        entry.Value("CallerId").Should().Be(CallerId);
        entry.Value("TargetUserId").Should().Be(TargetUserId);
        entry.Value("PreviousOrganizationId").Should().Be(OrgAId);
        entry.Value("NewOrganizationId").Should().Be(OrgBId);
    }

    [Fact]
    public async Task LogEvent_ForAClear_RecordsNoNewOrganization()
    {
        var (handler, _, orgRepo, _, log) = Make();
        orgRepo.Current = new Organization { Id = OrgAId, Name = "Org A" };

        await handler.HandleAsync(
            new AssignOrganizationCommand(TargetUserId, null, CallerId), CancellationToken.None);

        var entry = log.Entries.Should().ContainSingle().Subject;

        entry.Value("PreviousOrganizationId").Should().Be(OrgAId);
        entry.Value("NewOrganizationId").Should().BeNull(
            "clearing is logged the same way as an assignment, with no new organization");
    }

    [Fact]
    public async Task LogEvent_ContainsNoAuthenticationSecrets()
    {
        var (handler, _, orgRepo, _, log) = Make();
        orgRepo.Current = new Organization { Id = OrgAId, Name = "Org A" };

        await handler.HandleAsync(
            new AssignOrganizationCommand(TargetUserId, OrgBId, CallerId), CancellationToken.None);

        var rendered = log.Entries.Single().Rendered;

        // FR-019. The handler is only ever given identifiers, so this asserts the shape stays that
        // way — the failure it guards against is someone later logging the whole HttpContext or a
        // principal to make debugging easier.
        rendered.Should().NotContainAny(
            ["token", "Token", "cookie", "Cookie", "authorization", "Authorization", "Bearer", "secret"]);
    }

    // ── Fakes ─────────────────────────────────────────────────────────────

    private static (AssignOrganizationHandler Handler,
                    FakeUserRepo UserRepo,
                    FakeOrganizationRepo OrgRepo,
                    FakeSessionInvalidator Invalidator,
                    RecordingLogger<AssignOrganizationHandler> Log) Make()
    {
        var userRepo = new FakeUserRepo();
        var orgRepo = new FakeOrganizationRepo();
        var invalidator = new FakeSessionInvalidator();
        var log = new RecordingLogger<AssignOrganizationHandler>();
        return (new AssignOrganizationHandler(userRepo, orgRepo, invalidator, log),
                userRepo, orgRepo, invalidator, log);
    }

    private sealed class FakeUserRepo : IUserRepository
    {
        public bool Exists { get; set; } = true;

        public Task<bool> ExistsAsync(Guid userId, CancellationToken ct) => Task.FromResult(Exists);
        public Task<IReadOnlyList<(Guid Id, string DisplayName)>> GetAllAsync(CancellationToken ct)
            => Task.FromResult<IReadOnlyList<(Guid, string)>>([]);
        public Task<IReadOnlyList<AdminUserDto>> GetUsersWithRolesAndCharactersAsync(CancellationToken ct)
            => Task.FromResult<IReadOnlyList<AdminUserDto>>([]);
        public Task SetRolesAsync(Guid userId, IReadOnlyList<string> roles, CancellationToken ct)
            => Task.CompletedTask;
        public Task<IReadOnlyList<string>> GetRolesAsync(Guid userId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<string>>([]);
    }

    private sealed class FakeOrganizationRepo : IOrganizationRepository
    {
        public bool Exists { get; set; } = true;
        public Organization? Current { get; set; }
        public (Guid UserId, Guid? OrganizationId)? LastSet { get; private set; }

        public Task<IReadOnlyList<Organization>> GetAllAsync(CancellationToken ct)
            => Task.FromResult<IReadOnlyList<Organization>>([]);
        public Task<bool> ExistsAsync(Guid organizationId, CancellationToken ct)
            => Task.FromResult(Exists);
        public Task<Organization?> GetCurrentForUserAsync(Guid userId, CancellationToken ct)
            => Task.FromResult(Current);
        public Task<Guid?> SetCurrentAsync(Guid userId, Guid? organizationId, CancellationToken ct)
        {
            LastSet = (userId, organizationId);
            // The write reports the organization it replaced; the handler logs that as "previous".
            return Task.FromResult(Current?.Id);
        }
    }

    private sealed class FakeSessionInvalidator : IUserSessionInvalidator
    {
        public List<Guid> Invalidated { get; } = [];
        public void Invalidate(Guid userId) => Invalidated.Add(userId);
        public bool IsStaleSince(Guid userId, DateTimeOffset refreshedAt) => false;
    }

    /// <summary>
    /// Captures log entries with their structured state, so a test can assert on the named values
    /// FR-018 requires rather than on a formatted string.
    /// </summary>
    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var pairs = state as IReadOnlyList<KeyValuePair<string, object?>> ?? [];
            Entries.Add(new LogEntry(formatter(state, exception), pairs));
        }
    }

    private sealed record LogEntry(string Rendered, IReadOnlyList<KeyValuePair<string, object?>> State)
    {
        /// <summary>The structured value logged under <paramref name="name"/>, or null if absent.</summary>
        public object? Value(string name) =>
            State.FirstOrDefault(p => p.Key == name).Value;
    }
}
