namespace NajaEcho.Application.Features.Admin.Organizations.GetOrganizations;

public sealed record GetOrganizationsQuery;

public sealed record OrganizationDto(Guid Id, string Name);
