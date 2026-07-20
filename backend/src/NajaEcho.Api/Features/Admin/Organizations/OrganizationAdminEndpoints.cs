using Microsoft.Extensions.Logging;
using NajaEcho.Api.Authorization;
using NajaEcho.Api.Features.Admin.Organizations.Contracts;
using NajaEcho.Application.Features.Admin.Organizations.GetOrganizations;

namespace NajaEcho.Api.Features.Admin.Organizations;

public static class OrganizationAdminEndpoints
{
    public static IEndpointRouteBuilder MapOrganizationAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/organizations")
            .RequireAuthorization(AuthorizationPolicies.Admin);

        group.MapGet("/", GetOrganizations);

        // No POST, PUT, or DELETE. FR-015 makes "no path exists to create, rename, or delete an
        // organization" a requirement of this release, not an oversight to be filled in later.

        return app;
    }

    private static async Task<IResult> GetOrganizations(
        GetOrganizationsHandler handler,
        ILogger<GetOrganizationsHandler> logger,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var callerId = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        var organizations = await handler.HandleAsync(new GetOrganizationsQuery(), ct);

        logger.LogInformation("GetAdminOrganizations caller={CallerId} outcome=success count={Count}",
            callerId, organizations.Count);

        return Results.Ok(new OrganizationListResponse(
            organizations.Select(o => new OrganizationSummaryResponse(o.Id, o.Name)).ToList()));
    }
}
