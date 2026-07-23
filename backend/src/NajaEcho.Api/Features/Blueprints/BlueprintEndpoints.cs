using System.Security.Claims;
using NajaEcho.Api.Features.Blueprints.Contracts;
using NajaEcho.Application.Features.Blueprints.GetMyBlueprints;
using NajaEcho.Application.Features.Blueprints.SearchBlueprints;
using NajaEcho.Application.Features.Blueprints.AddMyBlueprint;
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
            items.Select(i => new MyBlueprintListItemResponse(i.BlueprintId, i.ProductName, i.Type, i.IngredientCount)).ToList()));
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
                new MyBlueprintListItemResponse(item.BlueprintId, item.ProductName, item.Type, item.IngredientCount));
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

    private static bool TryGetUserId(ClaimsPrincipal user, out Guid userId)
    {
        var sub = user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out userId);
    }
}
