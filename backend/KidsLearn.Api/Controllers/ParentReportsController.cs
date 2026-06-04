using System.Security.Claims;
using MediatR;

public static class ParentReportsController
{
    public static RouteGroupBuilder MapParentReportsEndpoints(this RouteGroupBuilder parentApi)
    {
        parentApi.MapGet("/reports/children/{childId:guid}", async (ISender sender, ClaimsPrincipal user, Guid childId, DateTime? from, DateTime? to) =>
        {
            if (!ApiEndpointHelpers.TryResolveUserId(user, out var parentId))
            {
                return Results.Unauthorized();
            }

            var result = await sender.Send(new GetParentChildReportSummaryQuery(parentId, childId, from, to));
            return result.StatusCode switch
            {
                StatusCodes.Status200OK when result.Response is not null => Results.Ok(result.Response),
                StatusCodes.Status404NotFound => Results.NotFound(new { error = result.Error ?? "Child not found." }),
                _ => Results.Problem(result.Error ?? "Unexpected error.")
            };
        });

        parentApi.MapGet("/reports/children/{childId:guid}/export", async (ISender sender, ClaimsPrincipal user, Guid childId, string? format, DateTime? from, DateTime? to) =>
        {
            if (!ApiEndpointHelpers.TryResolveUserId(user, out var parentId))
            {
                return Results.Unauthorized();
            }

            var result = await sender.Send(new ExportParentChildReportCsvQuery(parentId, childId, format, from, to));
            return result.StatusCode switch
            {
                StatusCodes.Status200OK when result.FileBytes is not null && !string.IsNullOrWhiteSpace(result.FileName)
                    => Results.File(result.FileBytes, "text/csv", result.FileName),
                StatusCodes.Status400BadRequest => Results.BadRequest(new { error = result.Error ?? "Only format=csv is supported." }),
                StatusCodes.Status404NotFound => Results.NotFound(new { error = result.Error ?? "Child not found." }),
                _ => Results.Problem(result.Error ?? "Unexpected error.")
            };
        });

        return parentApi;
    }
}


