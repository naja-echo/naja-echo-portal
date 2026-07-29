namespace NajaEcho.Api.Features.Admin.Organizations.Contracts;

public sealed record OrganizationSummaryResponse(Guid Id, string Name);

public sealed record OrganizationListResponse(IReadOnlyList<OrganizationSummaryResponse> Organizations);

/// <summary>
/// Sets a member's current organization, or clears it when <paramref name="OrganizationId"/> is null.
/// </summary>
public sealed record AssignOrganizationRequest(Guid? OrganizationId);
