using System.Security.Claims;
using NajaEcho.Api.Authorization;
using NajaEcho.Api.Features.Loot.Contracts;
using NajaEcho.Application.Features.Loot.AddLootPoints;
using NajaEcho.Application.Features.Loot.AddOrgPoints;
using NajaEcho.Application.Features.Loot.GetDistribution;
using NajaEcho.Application.Features.Loot.GetMemberLedger;
using Serilog;

namespace NajaEcho.Api.Features.Loot;

public static class LootEndpoints
{
    public static IEndpointRouteBuilder MapLootEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/loot").RequireAuthorization();

        group.MapGet("/me", GetMyLedger);
        group.MapGet("/distribution", GetDistribution);
        group.MapGet("/{userId:guid}", GetMemberLedger);
        group.MapPost("/{userId:guid}/org-points", AddOrgPoints)
            .RequireAuthorization(AuthorizationPolicies.CrewResourceOfficer);
        group.MapPost("/{userId:guid}/loot-points", AwardLootPoints)
            .RequireAuthorization(AuthorizationPolicies.Quartermaster);

        return app;
    }

    private static async Task<IResult> GetMyLedger(
        ClaimsPrincipal user,
        GetMemberLedgerHandler handler,
        CancellationToken ct)
    {
        if (!TryGetUserId(user, out var callerId))
            return Results.Unauthorized();

        Log.Information("GetMyLedger callerId={CallerId}", callerId);

        try
        {
            var dto = await handler.HandleAsync(new GetMemberLedgerQuery(callerId), ct);
            return Results.Ok(MapMemberLedger(dto));
        }
        catch (MemberNotFoundException)
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Member not found.");
        }
    }

    private static async Task<IResult> GetDistribution(
        ClaimsPrincipal user,
        GetDistributionHandler handler,
        CancellationToken ct)
    {
        if (!TryGetUserId(user, out var callerId))
            return Results.Unauthorized();

        Log.Information("GetDistribution callerId={CallerId}", callerId);

        var rows = await handler.HandleAsync(new GetDistributionQuery(), ct);
        var response = new LootDistributionResponse(
            rows.Select(r => new DistributionRowResponse(
                r.MemberId, r.DisplayName, r.OrgPointsTotal, r.LootPointsTotal, r.ClaimPriority))
            .ToList());

        return Results.Ok(response);
    }

    private static async Task<IResult> GetMemberLedger(
        ClaimsPrincipal user,
        Guid userId,
        GetMemberLedgerHandler handler,
        CancellationToken ct)
    {
        if (!TryGetUserId(user, out var callerId))
            return Results.Unauthorized();

        Log.Information("GetMemberLedger callerId={CallerId} targetUserId={TargetUserId}", callerId, userId);

        try
        {
            var dto = await handler.HandleAsync(new GetMemberLedgerQuery(userId), ct);
            return Results.Ok(MapMemberLedger(dto));
        }
        catch (MemberNotFoundException)
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Member not found.");
        }
    }

    private static async Task<IResult> AddOrgPoints(
        ClaimsPrincipal user,
        Guid userId,
        AddLedgerEntryRequest body,
        AddOrgPointsHandler handler,
        CancellationToken ct)
    {
        if (!TryGetUserId(user, out var callerId))
            return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(body.Reason))
            return Results.Problem(statusCode: StatusCodes.Status422UnprocessableEntity, title: "Reason is required.");

        Log.Information("AddOrgPoints callerId={CallerId} targetUserId={TargetUserId} amount={Amount}", callerId, userId, body.Amount);

        try
        {
            var entry = await handler.HandleAsync(
                new AddOrgPointsCommand(userId, body.Amount, body.Reason, callerId), ct);

            Log.Information("AddOrgPoints callerId={CallerId} targetUserId={TargetUserId} outcome=succeeded entryId={EntryId}", callerId, userId, entry.Id);

            return Results.Created($"/api/loot/{userId}", new LedgerEntryResponse(
                entry.Id, entry.Amount, entry.Reason, callerId.ToString(), entry.CreatedAt));
        }
        catch (MemberNotFoundException)
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Member not found.");
        }
        catch (ArgumentException ex)
        {
            return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status422UnprocessableEntity, title: "Validation error.");
        }
    }

    private static async Task<IResult> AwardLootPoints(
        ClaimsPrincipal user,
        Guid userId,
        AddLedgerEntryRequest body,
        AddLootPointsHandler handler,
        CancellationToken ct)
    {
        if (!TryGetUserId(user, out var callerId))
            return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(body.Reason))
            return Results.Problem(statusCode: StatusCodes.Status422UnprocessableEntity, title: "Reason is required.");

        Log.Information("AwardLootPoints callerId={CallerId} targetUserId={TargetUserId} amount={Amount}", callerId, userId, body.Amount);

        try
        {
            var entry = await handler.HandleAsync(
                new AddLootPointsCommand(userId, body.Amount, body.Reason, callerId), ct);

            Log.Information("AwardLootPoints callerId={CallerId} targetUserId={TargetUserId} outcome=succeeded entryId={EntryId}", callerId, userId, entry.Id);

            return Results.Created($"/api/loot/{userId}", new LedgerEntryResponse(
                entry.Id, entry.Amount, entry.Reason, callerId.ToString(), entry.CreatedAt));
        }
        catch (MemberNotFoundException)
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Member not found.");
        }
        catch (ArgumentException ex)
        {
            return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status422UnprocessableEntity, title: "Validation error.");
        }
    }

    private static bool TryGetUserId(ClaimsPrincipal user, out Guid userId)
    {
        var sub = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out userId);
    }

    private static MemberLedgerResponse MapMemberLedger(MemberLedgerDto dto) =>
        new(
            dto.MemberId,
            dto.DisplayName,
            dto.OrgPoints.Select(e => new LedgerEntryResponse(e.Id, e.Amount, e.Reason, e.PostedBy, e.CreatedAt)).ToList(),
            dto.LootPoints.Select(e => new LedgerEntryResponse(e.Id, e.Amount, e.Reason, e.PostedBy, e.CreatedAt)).ToList(),
            dto.OrgPointsTotal,
            dto.LootPointsTotal,
            dto.ClaimPriority);
}
