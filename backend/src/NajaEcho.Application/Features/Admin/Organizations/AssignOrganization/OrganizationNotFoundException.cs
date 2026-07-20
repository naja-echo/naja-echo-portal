namespace NajaEcho.Application.Features.Admin.Organizations.AssignOrganization;

public sealed class OrganizationNotFoundException(Guid organizationId)
    : Exception($"Organization '{organizationId}' was not found.")
{
    public Guid OrganizationId { get; } = organizationId;
}
