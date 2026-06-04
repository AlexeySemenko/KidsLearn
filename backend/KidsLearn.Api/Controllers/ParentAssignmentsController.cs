using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

public static class ParentAssignmentsController
{
    public static RouteGroupBuilder MapParentAssignmentEndpoints(this RouteGroupBuilder parentApi)
    {
        parentApi.MapPost("/assignments", async (AppDbContext db, ClaimsPrincipal user, CreateAssignmentRequest request) =>
        {
            var parentId = ApiEndpointHelpers.ResolveUserId(user);
            if (!parentId.HasValue)
            {
                return Results.Unauthorized();
            }

            var child = await db.Children.FirstOrDefaultAsync(x => x.Id == request.ChildId && x.ParentId == parentId.Value);
            if (child is null)
            {
                return Results.BadRequest(new { error = "Child does not belong to current parent." });
            }

            var lesson = await db.Lessons.FirstOrDefaultAsync(x => x.Id == request.LessonId && x.CreatedBy == parentId.Value);
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
            var parentId = ApiEndpointHelpers.ResolveUserId(user);
            if (!parentId.HasValue)
            {
                return Results.Unauthorized();
            }

            var assignments = await assignmentReadService.ListForParentAsync(parentId.Value, childId);
            return Results.Ok(assignments);
        });

        parentApi.MapGet("/assignments/{assignmentId:guid}/for-solving", async (IAssignmentSolvingService solvingService, ClaimsPrincipal user, Guid assignmentId) =>
        {
            var parentId = ApiEndpointHelpers.ResolveUserId(user);
            if (!parentId.HasValue)
            {
                return Results.Unauthorized();
            }

            var result = await solvingService.GetForSolvingAsync(AssignmentAccessScope.Parent, parentId.Value, assignmentId);
            return ApiEndpointHelpers.ToHttpResult(result);
        });

        parentApi.MapPost("/assignments/{assignmentId:guid}/answers", async (IAssignmentSolvingService solvingService, ClaimsPrincipal user, Guid assignmentId, SubmitAssignmentAnswersRequest request) =>
        {
            var parentId = ApiEndpointHelpers.ResolveUserId(user);
            if (!parentId.HasValue)
            {
                return Results.Unauthorized();
            }

            var result = await solvingService.SubmitAnswersAsync(AssignmentAccessScope.Parent, parentId.Value, assignmentId, request);
            return ApiEndpointHelpers.ToHttpResult(result);
        });

        parentApi.MapPost("/assignments/{assignmentId:guid}/complete", async (IAssignmentSolvingService solvingService, ClaimsPrincipal user, Guid assignmentId) =>
        {
            var parentId = ApiEndpointHelpers.ResolveUserId(user);
            if (!parentId.HasValue)
            {
                return Results.Unauthorized();
            }

            var result = await solvingService.CompleteAsync(AssignmentAccessScope.Parent, parentId.Value, assignmentId);
            return ApiEndpointHelpers.ToHttpResult(result);
        });

        parentApi.MapGet("/results/{resultId:guid}", async (IAssignmentSolvingService solvingService, ClaimsPrincipal user, Guid resultId) =>
        {
            var parentId = ApiEndpointHelpers.ResolveUserId(user);
            if (!parentId.HasValue)
            {
                return Results.Unauthorized();
            }

            var result = await solvingService.GetResultAsync(AssignmentAccessScope.Parent, parentId.Value, resultId);
            return ApiEndpointHelpers.ToHttpResult(result);
        });

        return parentApi;
    }
}
