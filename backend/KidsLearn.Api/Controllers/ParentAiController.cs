using System.Security.Claims;
using MediatR;

public static class ParentAiController
{
    public static RouteGroupBuilder MapParentAiEndpoints(this RouteGroupBuilder parentApi)
    {
        parentApi.MapPost("/ai/lessons/generate", async (ISender sender, ClaimsPrincipal user, GenerateAiLessonRequest request, CancellationToken cancellationToken) =>
        {
            if (!ApiEndpointHelpers.TryResolveUserId(user, out var parentId))
            {
                return Results.Unauthorized();
            }

            var result = await sender.Send(new GenerateParentAiLessonCommand(parentId, request), cancellationToken);
            return result.StatusCode switch
            {
                StatusCodes.Status200OK when result.Response is not null => Results.Ok(result.Response),
                StatusCodes.Status400BadRequest => Results.BadRequest(new { error = result.Error ?? "Bad request." }),
                StatusCodes.Status401Unauthorized => Results.Unauthorized(),
                StatusCodes.Status404NotFound => Results.NotFound(new { error = result.Error ?? "Not found." }),
                StatusCodes.Status409Conflict => Results.Conflict(new { error = result.Error ?? "Conflict." }),
                StatusCodes.Status422UnprocessableEntity => Results.UnprocessableEntity(new { error = result.Error ?? "Unprocessable entity." }),
                _ => Results.Problem(result.Error ?? "Unexpected error.")
            };
        });

        parentApi.MapPost("/ai/lessons/{lessonId:guid}/edit", async (ISender sender, ClaimsPrincipal user, Guid lessonId, EditAiLessonRequest request, CancellationToken cancellationToken) =>
        {
            if (!ApiEndpointHelpers.TryResolveUserId(user, out var parentId))
            {
                return Results.Unauthorized();
            }

            var result = await sender.Send(new EditParentAiLessonCommand(parentId, lessonId, request), cancellationToken);
            return result.StatusCode switch
            {
                StatusCodes.Status200OK when result.Response is not null => Results.Ok(result.Response),
                StatusCodes.Status400BadRequest => Results.BadRequest(new { error = result.Error ?? "Bad request." }),
                StatusCodes.Status401Unauthorized => Results.Unauthorized(),
                StatusCodes.Status404NotFound => Results.NotFound(new { error = result.Error ?? "Not found." }),
                StatusCodes.Status409Conflict => Results.Conflict(new { error = result.Error ?? "Conflict." }),
                StatusCodes.Status422UnprocessableEntity => Results.UnprocessableEntity(new { error = result.Error ?? "Unprocessable entity." }),
                _ => Results.Problem(result.Error ?? "Unexpected error.")
            };
        });

        parentApi.MapGet("/ai/lessons/{lessonId:guid}/revisions", async (ISender sender, ClaimsPrincipal user, Guid lessonId, CancellationToken cancellationToken) =>
        {
            if (!ApiEndpointHelpers.TryResolveUserId(user, out var parentId))
            {
                return Results.Unauthorized();
            }

            var result = await sender.Send(new GetParentAiLessonRevisionsQuery(parentId, lessonId), cancellationToken);
            return result.StatusCode switch
            {
                StatusCodes.Status200OK when result.Response is not null => Results.Ok(result.Response),
                StatusCodes.Status404NotFound => Results.NotFound(new { error = result.Error ?? "Not found." }),
                _ => Results.Problem(result.Error ?? "Unexpected error.")
            };
        });

        return parentApi;
    }
}

