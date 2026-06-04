using System.Security.Claims;

public static class ChildController
{
    public static RouteGroupBuilder MapChildController(this RouteGroupBuilder apiV1)
    {
        var childApi = apiV1.MapGroup("/child")
            .RequireAuthorization("ChildOnly");

        childApi.MapGet("/assignments", async (AppDbContext db, IAssignmentReadService assignmentReadService, ClaimsPrincipal user) =>
        {
            var childId = await ApiEndpointHelpers.ResolveChildIdAsync(db, user);
            if (!childId.HasValue)
            {
                return Results.Unauthorized();
            }

            var assignments = await assignmentReadService.ListForChildAsync(childId.Value);
            return Results.Ok(assignments);
        });

        childApi.MapGet("/assignments/{assignmentId:guid}/for-solving", async (AppDbContext db, IAssignmentSolvingService solvingService, ClaimsPrincipal user, Guid assignmentId) =>
        {
            var childId = await ApiEndpointHelpers.ResolveChildIdAsync(db, user);
            if (!childId.HasValue)
            {
                return Results.Unauthorized();
            }

            var result = await solvingService.GetForSolvingAsync(AssignmentAccessScope.Child, childId.Value, assignmentId);
            return ApiEndpointHelpers.ToHttpResult(result);
        });

        childApi.MapPost("/assignments/{assignmentId:guid}/answers", async (AppDbContext db, IAssignmentSolvingService solvingService, ClaimsPrincipal user, Guid assignmentId, SubmitAssignmentAnswersRequest request) =>
        {
            var childId = await ApiEndpointHelpers.ResolveChildIdAsync(db, user);
            if (!childId.HasValue)
            {
                return Results.Unauthorized();
            }

            var result = await solvingService.SubmitAnswersAsync(AssignmentAccessScope.Child, childId.Value, assignmentId, request);
            return ApiEndpointHelpers.ToHttpResult(result);
        });

        childApi.MapPost("/assignments/{assignmentId:guid}/complete", async (AppDbContext db, IAssignmentSolvingService solvingService, ClaimsPrincipal user, Guid assignmentId) =>
        {
            var childId = await ApiEndpointHelpers.ResolveChildIdAsync(db, user);
            if (!childId.HasValue)
            {
                return Results.Unauthorized();
            }

            var result = await solvingService.CompleteAsync(AssignmentAccessScope.Child, childId.Value, assignmentId);
            return ApiEndpointHelpers.ToHttpResult(result);
        });

        childApi.MapGet("/results/{resultId:guid}", async (AppDbContext db, IAssignmentSolvingService solvingService, ClaimsPrincipal user, Guid resultId) =>
        {
            var childId = await ApiEndpointHelpers.ResolveChildIdAsync(db, user);
            if (!childId.HasValue)
            {
                return Results.Unauthorized();
            }

            var result = await solvingService.GetResultAsync(AssignmentAccessScope.Child, childId.Value, resultId);
            return ApiEndpointHelpers.ToHttpResult(result);
        });

        return apiV1;
    }
}
