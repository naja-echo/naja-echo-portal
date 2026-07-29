namespace NajaEcho.Application.Features.Admin.Users.GetUsers;

public sealed record AdminUserOrganizationDto(Guid Id, string Name);

public sealed record AdminUserDto(
    Guid Id,
    string AuthName,
    IReadOnlyList<string> Roles,
    IReadOnlyList<AdminUserCharacterDto> Characters,
    AdminUserOrganizationDto? Organization);
