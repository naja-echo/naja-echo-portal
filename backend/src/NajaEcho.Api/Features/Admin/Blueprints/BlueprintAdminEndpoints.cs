using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using NajaEcho.Api.Authorization;
using NajaEcho.Api.Features.Admin.Blueprints.Contracts;
using NajaEcho.Application.Features.Blueprints.GetBlueprints;
using NajaEcho.Application.Features.Blueprints.ImportBlueprints;
using NajaEcho.Application.Features.Ships.ImportShips;

namespace NajaEcho.Api.Features.Admin.Blueprints;

public static class BlueprintAdminEndpoints
{
    // 50 MB upload cap (research Decision 2); Kestrel default is ~30 MB.
    private const long MaxUploadBytes = 50L * 1024 * 1024;

    public static IEndpointRouteBuilder MapBlueprintAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/blueprints").RequireAuthorization(AuthorizationPolicies.Admin);

        group.MapPost("/import", ImportBlueprints)
            .WithMetadata(new RequestSizeLimitAttribute(MaxUploadBytes));

        group.MapGet("/", GetBlueprints);

        return app;
    }

    private static async Task<IResult> ImportBlueprints(
        JsonDocument? body,
        ImportBlueprintsHandler handler,
        CancellationToken ct)
    {
        if (body is null)
        {
            return Results.Problem(
                detail: "The request body must be a JSON blueprint dataset document.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid dataset document.");
        }

        try
        {
            var result = await handler.HandleAsync(new ImportBlueprintsCommand(body.RootElement), ct);
            return Results.Ok(MapResult(result));
        }
        catch (InvalidBlueprintDocumentException ex)
        {
            return Results.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid dataset document.");
        }
        catch (ImportAlreadyInProgressException)
        {
            return Results.Conflict(new { title = "An import or refresh is already in progress.", status = 409 });
        }
        finally
        {
            body?.Dispose();
        }
    }

    private static async Task<IResult> GetBlueprints(
        GetBlueprintsHandler handler,
        CancellationToken ct)
    {
        var items = await handler.HandleAsync(new GetBlueprintsQuery(), ct);

        var response = new BlueprintListResponse(items
            .Select(i => new BlueprintListItemResponse(
                i.Guid, i.DisplayName, i.NameSource, i.ProductName, i.Tag, i.Manufacturer))
            .ToList());

        return Results.Ok(response);
    }

    private static ImportBlueprintsResponse MapResult(ImportBlueprintsResult result) =>
        new(
            result.Version,
            Map(result.Blueprints),
            Map(result.Resources),
            Map(result.Items),
            Map(result.Properties),
            result.ReferenceDataReplaced,
            result.Warnings,
            result.Rejections
                .Select(r => new BlueprintRejectionResponse(r.Guid, r.ProductName, r.Reason))
                .ToList());

    private static CollectionCountsResponse Map(CollectionCounts c) =>
        new(c.Read, c.Inserted, c.Updated, c.Rejected);
}
