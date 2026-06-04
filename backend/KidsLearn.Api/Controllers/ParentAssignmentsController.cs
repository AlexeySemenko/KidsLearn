using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

public static class ParentAssignmentsController
{
    public static RouteGroupBuilder MapParentAssignmentEndpoints(this RouteGroupBuilder parentApi)
    {
        parentApi.MapPost("/assignments", async (AppDbContext db, ClaimsPrincipal user, CreateAssignmentRequest request) =>
        {
            if (!ApiEndpointHelpers.TryResolveUserId(user, out var parentId))
            {
                return Results.Unauthorized();
            }

            var child = await db.Children.FirstOrDefaultAsync(x => x.Id == request.ChildId && x.ParentId == parentId);
            if (child is null)
            {
                return Results.BadRequest(new { error = "Child does not belong to current parent." });
            }

            var lesson = await db.Lessons.FirstOrDefaultAsync(x => x.Id == request.LessonId && x.CreatedBy == parentId);
            if (lesson is null)
            {
                return Results.BadRequest(new { error = "Lesson does not belong to current parent." });
            }

            var assignment = new Assignment
            {
                ChildId = request.ChildId,
                LessonId = request.LessonId,
                AssignedAt = DateTime.UtcNow,
                DueDate = request.DueDate,
                Status = "Assigned"
            };

            db.Assignments.Add(assignment);
            await db.SaveChangesAsync();

            return Results.Created($"/api/v1/assignments/{assignment.Id}", new AssignmentResponse(
                assignment.Id,
                assignment.ChildId,
                assignment.LessonId,
                assignment.AssignedAt,
                assignment.DueDate,
                assignment.Status));
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

        parentApi.MapPost("/assignments/{assignmentId:guid}/answers", async (IAssignmentSolvingService solvingService, ClaimsPrincipal user, Guid assignmentId, SubmitAssignmentAnswersRequest request) =>
        {
            if (!ApiEndpointHelpers.TryResolveUserId(user, out var parentId))
            {
                return Results.Unauthorized();
            }

            var result = await solvingService.SubmitAnswersAsync(AssignmentAccessScope.Parent, parentId, assignmentId, request);
            return ApiEndpointHelpers.ToHttpResult(result);
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


