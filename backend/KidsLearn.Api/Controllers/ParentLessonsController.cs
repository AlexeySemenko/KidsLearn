using System.Security.Claims;
using MediatR;

public static class ParentLessonsController
{
    public static RouteGroupBuilder MapParentLessonsEndpoints(this RouteGroupBuilder parentApi)
    {
        parentApi.MapPost("/lessons", async (ISender sender, ClaimsPrincipal user, CreateLessonRequest request) =>
        {
            if (!ApiEndpointHelpers.TryResolveUserId(user, out var parentId))
            {
                return Results.Unauthorized();
            }

            var result = await sender.Send(new CreateParentLessonCommand(parentId, request));
            if (result.Lesson is null)
            {
                return result.StatusCode switch
                {
                    StatusCodes.Status400BadRequest => Results.BadRequest(new { error = result.Error ?? "Bad request." }),
                    _ => Results.Problem(result.Error ?? "Unexpected error.")
                };
            }

            return Results.Created($"/api/v1/lessons/{result.Lesson.Id}", result.Lesson);
        });

        parentApi.MapGet("/lessons", async (ISender sender, ClaimsPrincipal user, string? subject, int? grade, string? topic, int page = 1, int pageSize = 20) =>
        {
            if (!ApiEndpointHelpers.TryResolveUserId(user, out var parentId))
            {
                return Results.Unauthorized();
            }

            var result = await sender.Send(new GetParentLessonsQuery(parentId, subject, grade, topic, page, pageSize));
            return Results.Ok(new { items = result.Items, total = result.Total, page = result.Page, pageSize = result.PageSize });
        });

        parentApi.MapGet("/lessons/{lessonId:guid}", async (ISender sender, ClaimsPrincipal user, Guid lessonId) =>
        {
            if (!ApiEndpointHelpers.TryResolveUserId(user, out var parentId))
            {
                return Results.Unauthorized();
            }

            var result = await sender.Send(new GetParentLessonDetailQuery(parentId, lessonId));
            return result.StatusCode switch
            {
                StatusCodes.Status200OK when result.Lesson is not null => Results.Ok(result.Lesson),
                StatusCodes.Status404NotFound => Results.NotFound(),
                _ => Results.Problem("Unexpected error.")
            };
        });

        parentApi.MapPost("/lessons/{lessonId:guid}/duplicate", async (ISender sender, ClaimsPrincipal user, Guid lessonId) =>
        {
            if (!ApiEndpointHelpers.TryResolveUserId(user, out var parentId))
            {
                return Results.Unauthorized();
            }

            var result = await sender.Send(new DuplicateParentLessonCommand(parentId, lessonId));
            if (result.Lesson is null)
            {
                return result.StatusCode switch
                {
                    StatusCodes.Status400BadRequest => Results.BadRequest(new { error = result.Error ?? "Bad request." }),
                    StatusCodes.Status404NotFound => Results.NotFound(new { error = result.Error ?? "Not found." }),
                    _ => Results.Problem(result.Error ?? "Unexpected error.")
                };
            }

            return Results.Created($"/api/v1/lessons/{result.Lesson.Id}", result.Lesson);
        });

        parentApi.MapPatch("/lessons/{lessonId:guid}", async (ISender sender, ClaimsPrincipal user, Guid lessonId, UpdateLessonRequest request) =>
        {
            if (!ApiEndpointHelpers.TryResolveUserId(user, out var parentId))
            {
                return Results.Unauthorized();
            }

            var result = await sender.Send(new UpdateParentLessonCommand(parentId, lessonId, request));
            if (result.Lesson is null)
            {
                return result.StatusCode switch
                {
                    StatusCodes.Status400BadRequest => Results.BadRequest(new { error = result.Error ?? "Bad request." }),
                    StatusCodes.Status404NotFound => Results.NotFound(new { error = result.Error ?? "Not found." }),
                    _ => Results.Problem(result.Error ?? "Unexpected error.")
                };
            }

            return Results.Ok(result.Lesson);
        });

        parentApi.MapDelete("/lessons/{lessonId:guid}", async (ISender sender, ClaimsPrincipal user, Guid lessonId) =>
        {
            if (!ApiEndpointHelpers.TryResolveUserId(user, out var parentId))
            {
                return Results.Unauthorized();
            }

            var result = await sender.Send(new DeleteParentLessonCommand(parentId, lessonId));
            return result.StatusCode switch
            {
                StatusCodes.Status400BadRequest => Results.BadRequest(new { error = result.Error ?? "Bad request." }),
                StatusCodes.Status404NotFound => Results.NotFound(new { error = result.Error ?? "Not found." }),
                StatusCodes.Status409Conflict => Results.Conflict(new { error = result.Error ?? "Conflict." }),
                StatusCodes.Status204NoContent => Results.NoContent(),
                _ => Results.Problem(result.Error ?? "Unexpected error.")
            };
        });

        return parentApi;
    }
}


