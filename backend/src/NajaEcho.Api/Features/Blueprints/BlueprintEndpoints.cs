using System.Security.Claims;
using NajaEcho.Api.Features.Blueprints.Contracts;
using NajaEcho.Application.Features.Blueprints.AddMyBlueprint;
using NajaEcho.Application.Features.Blueprints.GetBlueprintDetail;
using NajaEcho.Application.Features.Blueprints.GetMyBlueprints;
using NajaEcho.Application.Features.Blueprints.GetOrgBlueprintDetail;
using NajaEcho.Application.Features.Blueprints.GetOrgBlueprints;
using NajaEcho.Application.Features.Blueprints.RemoveMyBlueprint;
using NajaEcho.Application.Features.Blueprints.SearchBlueprints;
using NajaEcho.Domain.Blueprints;
using Serilog;

namespace NajaEcho.Api.Features.Blueprints;

public static class BlueprintEndpoints
{
    public static IEndpointRouteBuilder MapBlueprintEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/blueprints").RequireAuthorization();

        group.MapGet("/search", SearchBlueprints);
        group.MapGet("/mine", GetMyBlueprints);
        group.MapPost("/mine", AddMyBlueprint);
        group.MapGet("/mine/{blueprintId:guid}", GetMyBlueprintDetail);
        group.MapDelete("/mine/{blueprintId:guid}", RemoveMyBlueprint);
        group.MapGet("/org", GetOrgBlueprints);
        group.MapGet("/org/{blueprintId:guid}", GetOrgBlueprintDetail);

        return app;
    }

    private static async Task<IResult> SearchBlueprints(
        ClaimsPrincipal user,
        SearchBlueprintsHandler handler,
        string? q = null,
        CancellationToken ct = default)
    {
        if (!TryGetUserId(user, out var userId))
        {
            return Results.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(q))
        {
            return Results.Ok(new BlueprintSearchResponse([]));
        }

        Log.Information("SearchBlueprints {UserId} q={Query}", userId, q);

        var results = await handler.HandleAsync(new SearchBlueprintsQuery(q), ct);

        return Results.Ok(new BlueprintSearchResponse(
            results.Select(r => new BlueprintSearchResultResponse(r.BlueprintId, r.ProductName, r.Type)).ToList()));
    }

    private static async Task<IResult> GetMyBlueprints(
        ClaimsPrincipal user,
        GetMyBlueprintsHandler handler,
        CancellationToken ct = default)
    {
        if (!TryGetUserId(user, out var userId))
        {
            return Results.Unauthorized();
        }

        Log.Information("GetMyBlueprints {UserId}", userId);

        var items = await handler.HandleAsync(new GetMyBlueprintsQuery(userId), ct);

        Log.Information("GetMyBlueprints {UserId} returned {Count}", userId, items.Count);

        return Results.Ok(new MyBlueprintListResponse(
            items.Select(i => new MyBlueprintListItemResponse(i.BlueprintId, i.ProductName, i.Type, i.Subtype, i.Gear, i.Tag, i.ComponentClass, i.ComponentSize, i.ComponentGrade, i.IngredientCount)).ToList()));
    }

    private static async Task<IResult> AddMyBlueprint(
        ClaimsPrincipal user,
        AddMyBlueprintRequest body,
        AddMyBlueprintHandler handler,
        CancellationToken ct = default)
    {
        if (!TryGetUserId(user, out var userId))
        {
            return Results.Unauthorized();
        }

        Log.Information("AddMyBlueprint {UserId} blueprintId={BlueprintId}", userId, body.BlueprintId);

        try
        {
            var item = await handler.HandleAsync(new AddMyBlueprintCommand(userId, body.BlueprintId), ct);

            Log.Information("AddMyBlueprint succeeded {UserId} blueprintId={BlueprintId}", userId, body.BlueprintId);

            return Results.Created(
                $"/api/blueprints/mine",
                new MyBlueprintListItemResponse(item.BlueprintId, item.ProductName, item.Type, item.Subtype, item.Gear, item.Tag, item.ComponentClass, item.ComponentSize, item.ComponentGrade, item.IngredientCount));
        }
        catch (BlueprintNotFoundException ex)
        {
            Log.Warning("AddMyBlueprint 404 {UserId} {BlueprintId}: {Message}", userId, body.BlueprintId, ex.Message);
            return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status404NotFound, title: "Blueprint not found.");
        }
        catch (DuplicateBlueprintException ex)
        {
            Log.Warning("AddMyBlueprint 409 {UserId} {BlueprintId}: {Message}", userId, body.BlueprintId, ex.Message);
            return Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict, title: "Blueprint already in your list.");
        }
    }

    private static async Task<IResult> GetMyBlueprintDetail(
        ClaimsPrincipal user,
        Guid blueprintId,
        GetBlueprintDetailHandler handler,
        CancellationToken ct = default)
    {
        if (!TryGetUserId(user, out var userId))
            return Results.Unauthorized();

        Log.Information("GetMyBlueprintDetail {UserId} blueprintId={BlueprintId}", userId, blueprintId);

        var detail = await handler.HandleAsync(new GetBlueprintDetailQuery(userId, blueprintId), ct);

        if (detail is null)
            return Results.NotFound();

        return Results.Ok(new BlueprintDetailResponse(
            detail.BlueprintId,
            detail.ProductName,
            detail.Type,
            detail.CraftTimeSeconds,
            detail.IngredientCount,
            detail.ComponentClass,
            detail.ComponentSize,
            detail.ComponentGrade,
            detail.Slots.Select(s => new BlueprintSlotResponse(
                s.SlotIndex,
                s.SlotName,
                s.Options.Select(o => new BlueprintSlotOptionResponse(o.OptionIndex, o.MaterialName, o.Kind, o.Quantity)).ToList()
            )).ToList()));
    }

    private static async Task<IResult> RemoveMyBlueprint(
        ClaimsPrincipal user,
        Guid blueprintId,
        RemoveMyBlueprintHandler handler,
        CancellationToken ct = default)
    {
        if (!TryGetUserId(user, out var userId))
            return Results.Unauthorized();

        Log.Information("RemoveMyBlueprint {UserId} blueprintId={BlueprintId}", userId, blueprintId);

        var removed = await handler.HandleAsync(new RemoveMyBlueprintCommand(userId, blueprintId), ct);

        if (!removed)
            return Results.NotFound();

        Log.Information("RemoveMyBlueprint succeeded {UserId} blueprintId={BlueprintId}", userId, blueprintId);
        return Results.NoContent();
    }

    private static async Task<IResult> GetOrgBlueprints(
        ClaimsPrincipal user,
        GetOrgBlueprintsHandler handler,
        CancellationToken ct = default)
    {
        if (!TryGetUserId(user, out var userId))
            return Results.Unauthorized();

        Log.Information("GetOrgBlueprints {UserId}", userId);

        var items = await handler.HandleAsync(new GetOrgBlueprintsQuery(userId), ct);

        return Results.Ok(new OrgBlueprintListResponse(
            items.Select(i => new OrgBlueprintListItemResponse(i.BlueprintId, i.ProductName, i.Type, i.Subtype, i.Gear, i.Tag, i.ComponentClass, i.ComponentSize, i.ComponentGrade, i.IngredientCount)).ToList()));
    }

    private static async Task<IResult> GetOrgBlueprintDetail(
        ClaimsPrincipal user,
        Guid blueprintId,
        GetOrgBlueprintDetailHandler handler,
        CancellationToken ct = default)
    {
        if (!TryGetUserId(user, out var userId))
            return Results.Unauthorized();

        Log.Information("GetOrgBlueprintDetail {UserId} blueprintId={BlueprintId}", userId, blueprintId);

        var detail = await handler.HandleAsync(new GetOrgBlueprintDetailQuery(userId, blueprintId), ct);

        if (detail is null)
            return Results.NotFound();

        return Results.Ok(new OrgBlueprintDetailResponse(
            detail.BlueprintId,
            detail.ProductName,
            detail.Type,
            detail.CraftTimeSeconds,
            detail.IngredientCount,
            detail.ComponentClass,
            detail.ComponentSize,
            detail.ComponentGrade,
            detail.Slots.Select(s => new BlueprintSlotResponse(
                s.SlotIndex,
                s.SlotName,
                s.Options.Select(o => new BlueprintSlotOptionResponse(o.OptionIndex, o.MaterialName, o.Kind, o.Quantity)).ToList()
            )).ToList(),
            detail.Owners.Select(o => new BlueprintOwnerResponse(o.UserId, o.DisplayName)).ToList()));
    }

    private static bool TryGetUserId(ClaimsPrincipal user, out Guid userId)
    {
        var sub = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out userId);
    }
}
