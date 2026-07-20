using NajaEcho.Api.Features.Admin.Organizations.Contracts;

namespace NajaEcho.Api.Features.Admin.Users.Contracts;

public sealed record AdminUserCharacterResponse(Guid Id, string Name, string Handle);

public sealed record AdminUserResponse(
    Guid Id,
    string AuthName,
    IReadOnlyList<string> Roles,
    IReadOnlyList<AdminUserCharacterResponse> Characters,
    OrganizationSummaryResponse? Organization);

public sealed record AdminUserListResponse(IReadOnlyList<AdminUserResponse> Users);
