using Microsoft.Extensions.Logging;
using NajaEcho.Application.Abstractions;

namespace NajaEcho.Application.Features.Admin.Organizations.GetOrganizations;

/// <summary>
/// Lists the organizations a member may be assigned to.
/// </summary>
/// <remarks>
/// This release ships only the default "Naja Echo" organization and offers no way to create,
/// rename, or delete one (spec FR-015), so the result is expected to hold exactly one entry. The
/// endpoint returns a list anyway rather than a single value, because #32–#34 arrive into a world
/// where more than one may exist and a shape change then would ripple through the UI.
/// </remarks>
public sealed class GetOrganizationsHandler(
    IOrganizationRepository organizationRepository,
    ILogger<GetOrganizationsHandler> logger)
{
    public async Task<IReadOnlyList<OrganizationDto>> HandleAsync(
        GetOrganizationsQuery query, CancellationToken ct)
    {
        var organizations = await organizationRepository.GetAllAsync(ct);

        logger.LogInformation("GetOrganizations returned {Count} organizations", organizations.Count);

        return organizations.Select(o => new OrganizationDto(o.Id, o.Name)).ToList();
    }
}
