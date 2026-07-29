using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NajaEcho.Application.Abstractions;
using NajaEcho.Application.Features.Admin.Users.AddCharacterForUser;
using NajaEcho.Application.Features.Characters.VerifyCharacter;
using NajaEcho.Domain.Characters;
using NajaEcho.Domain.Organizations;

namespace NajaEcho.Application.Tests.Features.Admin.Users;

public sealed class AddCharacterForUserHandlerTests
{
    private static readonly Guid TargetUserId = Guid.NewGuid();
    private const string ValidHandle = "testhandle";

    private sealed class FakeUserRepo(bool exists) : IUserRepository
    {
        public Task<bool> ExistsAsync(Guid userId, CancellationToken ct) => Task.FromResult(exists);
        public Task<IReadOnlyList<(Guid Id, string DisplayName)>> GetAllAsync(CancellationToken ct)
            => Task.FromResult<IReadOnlyList<(Guid, string)>>([]);
        public Task<IReadOnlyList<Application.Features.Admin.Users.GetUsers.AdminUserDto>> GetUsersWithRolesAndCharactersAsync(CancellationToken ct)
            => Task.FromResult<IReadOnlyList<Application.Features.Admin.Users.GetUsers.AdminUserDto>>([]);
        public Task SetRolesAsync(Guid userId, IReadOnlyList<string> roles, CancellationToken ct) => Task.CompletedTask;


        public Task<IReadOnlyList<string>> GetRolesAsync(Guid userId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<string>>([]);
    }

    private sealed class FakeCharacterRepo : ICharacterRepository
    {
        public bool HandleExists { get; set; }
        public Character? Added { get; private set; }
        public Task<bool> HandleExistsAsync(string handle, CancellationToken ct) => Task.FromResult(HandleExists);
        public Task AddAsync(Character character, CancellationToken ct)
        {
            Added = character;
            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<Character>> GetByOwnerAsync(Guid ownerUserId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<Character>>([]);
    }

    private sealed class FakeRsiClient(object result) : IRsiCitizenClient
    {
        public Task<object> FetchCitizenAsync(string handle, CancellationToken ct) => Task.FromResult(result);
    }

    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }

    private static AddCharacterForUserHandler MakeHandler(
        bool userExists = true,
        bool handleExists = false,
        object? rsiResult = null,
        FakeCharacterRepo? charRepo = null)
    {
        var rsi = rsiResult ?? new RsiCitizenPage("content", "CharacterName");
        return new AddCharacterForUserHandler(
            new FakeUserRepo(userExists),
            charRepo ?? new FakeCharacterRepo { HandleExists = handleExists },
            new FakeRsiClient(rsi),
            new FakeClock(),
            NullLogger<AddCharacterForUserHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_HappyPath_CreatesCharacterLinkedToTargetUser()
    {
        var charRepo = new FakeCharacterRepo();
        var handler = MakeHandler(charRepo: charRepo);

        var result = await handler.HandleAsync(
            new AddCharacterForUserCommand(TargetUserId, ValidHandle), default);

        result.Name.Should().Be("CharacterName");
        result.Handle.Should().Be(ValidHandle);
        charRepo.Added.Should().NotBeNull();
        charRepo.Added!.OwnerUserId.Should().Be(TargetUserId);
    }

    [Fact]
    public async Task HandleAsync_BlankHandle_ThrowsArgumentException()
    {
        var handler = MakeHandler();
        var act = () => handler.HandleAsync(new AddCharacterForUserCommand(TargetUserId, "  "), default);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task HandleAsync_UnknownTargetUser_ThrowsUserNotFoundException()
    {
        var handler = MakeHandler(userExists: false);
        var act = () => handler.HandleAsync(new AddCharacterForUserCommand(TargetUserId, ValidHandle), default);
        await act.Should().ThrowAsync<UserNotFoundException>();
    }

    [Fact]
    public async Task HandleAsync_DuplicateHandle_ThrowsHandleAlreadyClaimedException()
    {
        var handler = MakeHandler(handleExists: true);
        var act = () => handler.HandleAsync(new AddCharacterForUserCommand(TargetUserId, ValidHandle), default);
        await act.Should().ThrowAsync<HandleAlreadyClaimedException>();
    }

    [Fact]
    public async Task HandleAsync_RsiProfileNotFound_ThrowsRsiProfileNotFoundException()
    {
        var handler = MakeHandler(rsiResult: new RsiProfileNotFound());
        var act = () => handler.HandleAsync(new AddCharacterForUserCommand(TargetUserId, ValidHandle), default);
        await act.Should().ThrowAsync<RsiProfileNotFoundException>();
    }

    [Fact]
    public async Task HandleAsync_RsiUnreachable_ThrowsRsiUnreachableException()
    {
        var handler = MakeHandler(rsiResult: new RsiUnreachable());
        var act = () => handler.HandleAsync(new AddCharacterForUserCommand(TargetUserId, ValidHandle), default);
        await act.Should().ThrowAsync<RsiUnreachableException>();
    }

    [Fact]
    public async Task HandleAsync_BlankDisplayName_ThrowsCharacterNameUnavailableException()
    {
        var handler = MakeHandler(rsiResult: new RsiCitizenPage("content", null));
        var act = () => handler.HandleAsync(new AddCharacterForUserCommand(TargetUserId, ValidHandle), default);
        await act.Should().ThrowAsync<CharacterNameUnavailableException>();
    }

    [Fact]
    public async Task HandleAsync_WhitespaceDisplayName_ThrowsCharacterNameUnavailableException()
    {
        var handler = MakeHandler(rsiResult: new RsiCitizenPage("content", "   "));
        var act = () => handler.HandleAsync(new AddCharacterForUserCommand(TargetUserId, ValidHandle), default);
        await act.Should().ThrowAsync<CharacterNameUnavailableException>();
    }
}
