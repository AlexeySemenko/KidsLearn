using System.Security.Claims;
using MediatR;

public static class ParentAssignmentsController
{
    public static RouteGroupBuilder MapParentAssignmentsEndpoints(this RouteGroupBuilder parentApi)
    {
        parentApi.MapPost("/assignments", async (ISender sender, ClaimsPrincipal user, CreateAssignmentRequest request) =>
        {
            if (!ApiEndpointHelpers.TryResolveUserId(user, out var parentId))
            {
                return Results.Unauthorized();
            }

            var result = await sender.Send(new CreateParentAssignmentCommand(parentId, request));
            if (result.Assignment is null)
            {
                return result.StatusCode switch
                {
                    StatusCodes.Status400BadRequest => Results.BadRequest(new { error = result.Error ?? "Bad request." }),
                    _ => Results.Problem(result.Error ?? "Unexpected error.")
                };
            }

            return Results.Created($"/api/v1/assignments/{result.Assignment.Id}", result.Assignment);
        });

        parentApi.MapGet("/assignments", async (IAssignmentReadService assignmentReadService, ClaimsPrincipal user, Guid? childId) =>
        {
            if (!ApiEndpointHelpers.TryResolveUserId(user, out var parentId))
            {
                return Results.Unauthorized();
            }

            var assignments = await assignmentReadService.ListForParentAsync(parentId, childId);
            return Results.Ok(assignments);
        });

        parentApi.MapGet("/assignments/{assignmentId:guid}/for-solving", async (IAssignmentSolvingService solvingService, ClaimsPrincipal user, Guid assignmentId) =>
        {
            if (!ApiEndpointHelpers.TryResolveUserId(user, out var parentId))
            {
                return Results.Unauthorized();
            }

            var result = await solvingService.GetForSolvingAsync(AssignmentAccessScope.Parent, parentId, assignmentId);
            return ApiEndpointHelpers.ToHttpResult(result);
        });

        parentApi.MapPost("/assignments/{assignmentId:guid}/answers", async (ISender sender, ClaimsPrincipal user, Guid assignmentId, SubmitAssignmentAnswersRequest request) =>
        {
            if (!ApiEndpointHelpers.TryResolveUserId(user, out var parentId))
            {
                return Results.Unauthorized();
            }

            var result = await sender.Send(new SubmitParentAssignmentAnswersCommand(parentId, assignmentId, request));
            return result.StatusCode switch
            {
                StatusCodes.Status200OK when result.Response is not null => Results.Ok(result.Response),
                StatusCodes.Status400BadRequest => Results.BadRequest(new { error = result.Error ?? "Bad request." }),
                StatusCodes.Status404NotFound => Results.NotFound(new { error = result.Error ?? "Not found." }),
                StatusCodes.Status409Conflict => Results.Conflict(new { error = result.Error ?? "Conflict." }),
                StatusCodes.Status422UnprocessableEntity => Results.UnprocessableEntity(new { error = result.Error ?? "Unprocessable entity." }),
                _ => Results.Problem(result.Error ?? "Unexpected error.")
            };
        });

        parentApi.MapPost("/assignments/{assignmentId:guid}/complete", async (IAssignmentSolvingService solvingService, ClaimsPrincipal user, Guid assignmentId) =>
        {
            if (!ApiEndpointHelpers.TryResolveUserId(user, out var parentId))
            {
                return Results.Unauthorized();
            }

            var result = await solvingService.CompleteAsync(AssignmentAccessScope.Parent, parentId, assignmentId);
            return ApiEndpointHelpers.ToHttpResult(result);
        });

        parentApi.MapGet("/results/{resultId:guid}", async (IAssignmentSolvingService solvingService, ClaimsPrincipal user, Guid resultId) =>
        {
            if (!ApiEndpointHelpers.TryResolveUserId(user, out var parentId))
            {
                return Results.Unauthorized();
            }

            var result = await solvingService.GetResultAsync(AssignmentAccessScope.Parent, parentId, resultId);
            return ApiEndpointHelpers.ToHttpResult(result);
        });

        return parentApi;
    }
}


