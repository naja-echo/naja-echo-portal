using Microsoft.Extensions.Logging;
using NajaEcho.Api.Authorization;
using NajaEcho.Api.Features.Admin.Organizations.Contracts;
using NajaEcho.Api.Features.Admin.Users.Contracts;
using NajaEcho.Application.Features.Admin.Organizations.AssignOrganization;
using NajaEcho.Application.Features.Admin.Users.AddCharacterForUser;
using NajaEcho.Application.Features.Admin.Users.AssignRoles;
using NajaEcho.Application.Features.Admin.Users.GetUsers;
using NajaEcho.Application.Features.Characters.VerifyCharacter;

namespace NajaEcho.Api.Features.Admin.Users;

public static class UserAdminEndpoints
{
    public static IEndpointRouteBuilder MapUserAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/users").RequireAuthorization(AuthorizationPolicies.Admin);

        group.MapGet("/", GetUsers);
        group.MapPost("/{userId:guid}/characters", AddCharacterForUser);
        group.MapPut("/{userId:guid}/roles", AssignRoles);
        group.MapPut("/{userId:guid}/organization", AssignOrganization);

        return app;
    }

    private static async Task<IResult> GetUsers(
        GetUsersHandler handler,
        ILogger<GetUsersHandler> logger,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var callerId = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        var users = await handler.HandleAsync(new GetUsersQuery(), ct);

        logger.LogInformation("GetAdminUsers caller={CallerId} outcome=success count={Count}",
            callerId, users.Count);

        var response = new AdminUserListResponse(
            users.Select(u => new AdminUserResponse(
                u.Id,
                u.AuthName,
                u.Roles,
                u.Characters.Select(c => new AdminUserCharacterResponse(c.Id, c.Name, c.Handle)).ToList(),
                u.Organization is null
                    ? null
                    : new OrganizationSummaryResponse(u.Organization.Id, u.Organization.Name)))
            .ToList());

        return Results.Ok(response);
    }

    private static async Task<IResult> AddCharacterForUser(
        Guid userId,
        AddCharacterRequest request,
        AddCharacterForUserHandler handler,
        ILogger<AddCharacterForUserHandler> logger,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var callerId = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrWhiteSpace(request.Handle))
        {
            return Results.Problem(
                detail: "Handle is required.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Bad Request");
        }

        try
        {
            var character = await handler.HandleAsync(
                new AddCharacterForUserCommand(userId, request.Handle), ct);

            logger.LogInformation(
                "AddCharacterForUser caller={CallerId} targetUserId={TargetUserId} handle={Handle} outcome=success characterId={CharacterId}",
                callerId, userId, request.Handle, character.Id);

            return Results.Created(
                $"/api/admin/users/{userId}/characters/{character.Id}",
                new AdminUserCharacterResponse(character.Id, character.Name, character.Handle));
        }
        catch (UserNotFoundException)
        {
            logger.LogInformation(
                "AddCharacterForUser caller={CallerId} targetUserId={TargetUserId} handle={Handle} outcome=user-not-found",
                callerId, userId, request.Handle);

            return Results.Problem(
                detail: $"User '{userId}' was not found.",
                statusCode: StatusCodes.Status404NotFound,
                title: "User not found.",
                type: "urn:najaecho:error:user-not-found");
        }
        catch (HandleAlreadyClaimedException)
        {
            logger.LogInformation(
                "AddCharacterForUser caller={CallerId} targetUserId={TargetUserId} handle={Handle} outcome=already-claimed",
                callerId, userId, request.Handle);

            return Results.Problem(
                detail: "This handle is already registered to a member.",
                statusCode: StatusCodes.Status409Conflict,
                title: "Handle already claimed.");
        }
        catch (RsiProfileNotFoundException)
        {
            logger.LogInformation(
                "AddCharacterForUser caller={CallerId} targetUserId={TargetUserId} handle={Handle} outcome=rsi-not-found",
                callerId, userId, request.Handle);

            return Results.Problem(
                detail: "The RSI citizen page for this handle does not exist.",
                statusCode: StatusCodes.Status404NotFound,
                title: "RSI handle not found.",
                type: "urn:najaecho:error:rsi-handle-not-found");
        }
        catch (RsiUnreachableException)
        {
            logger.LogInformation(
                "AddCharacterForUser caller={CallerId} targetUserId={TargetUserId} handle={Handle} outcome=rsi-unreachable",
                callerId, userId, request.Handle);

            return Results.Problem(
                detail: "RSI could not be reached or returned an unusable response.",
                statusCode: StatusCodes.Status502BadGateway,
                title: "RSI unreachable.");
        }
        catch (CharacterNameUnavailableException)
        {
            logger.LogInformation(
                "AddCharacterForUser caller={CallerId} targetUserId={TargetUserId} handle={Handle} outcome=name-unavailable",
                callerId, userId, request.Handle);

            return Results.Problem(
                detail: "Character name could not be retrieved — the handle may be valid but the RSI page returned no name.",
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "Character name unavailable.");
        }
    }

    private static async Task<IResult> AssignOrganization(
        Guid userId,
        AssignOrganizationRequest request,
        AssignOrganizationHandler handler,
        ILogger<AssignOrganizationHandler> logger,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var callerIdValue = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var callerId = Guid.TryParse(callerIdValue, out var parsed) ? parsed : (Guid?)null;

        try
        {
            await handler.HandleAsync(
                new AssignOrganizationCommand(userId, request.OrganizationId, callerId), ct);

            // The handler emits the FR-018 audit event; this is the endpoint-level outcome line
            // that every other endpoint in this group also writes.
            logger.LogInformation(
                "AssignOrganization caller={CallerId} targetUserId={TargetUserId} organizationId={OrganizationId} outcome=success",
                callerIdValue, userId, request.OrganizationId);

            return Results.NoContent();
        }
        catch (UserNotFoundException)
        {
            logger.LogInformation(
                "AssignOrganization caller={CallerId} targetUserId={TargetUserId} outcome=user-not-found",
                callerIdValue, userId);

            return Results.Problem(
                detail: $"User '{userId}' was not found.",
                statusCode: StatusCodes.Status404NotFound,
                title: "User not found.",
                type: "urn:najaecho:error:user-not-found");
        }
        catch (OrganizationNotFoundException ex)
        {
            logger.LogInformation(
                "AssignOrganization caller={CallerId} targetUserId={TargetUserId} organizationId={OrganizationId} outcome=organization-not-found",
                callerIdValue, userId, ex.OrganizationId);

            return Results.Problem(
                detail: $"Organization '{ex.OrganizationId}' was not found.",
                statusCode: StatusCodes.Status404NotFound,
                title: "Organization not found.",
                type: "urn:najaecho:error:organization-not-found");
        }
    }

    private static async Task<IResult> AssignRoles(
        Guid userId,
        AssignRolesRequest request,
        AssignRolesHandler handler,
        ILogger<AssignRolesHandler> logger,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var callerId = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        try
        {
            await handler.HandleAsync(new AssignRolesCommand(userId, request.Roles), ct);

            logger.LogInformation(
                "AssignRoles caller={CallerId} targetUserId={TargetUserId} roles=[{Roles}] outcome=success",
                callerId, userId, string.Join(", ", request.Roles));

            return Results.NoContent();
        }
        catch (UserNotFoundException)
        {
            logger.LogInformation(
                "AssignRoles caller={CallerId} targetUserId={TargetUserId} outcome=user-not-found",
                callerId, userId);

            return Results.Problem(
                detail: $"User '{userId}' was not found.",
                statusCode: StatusCodes.Status404NotFound,
                title: "User not found.",
                type: "urn:najaecho:error:user-not-found");
        }
        catch (InvalidRoleException ex)
        {
            logger.LogInformation(
                "AssignRoles caller={CallerId} targetUserId={TargetUserId} outcome=invalid-role message={Message}",
                callerId, userId, ex.Message);

            return Results.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid role.");
        }
    }
}
