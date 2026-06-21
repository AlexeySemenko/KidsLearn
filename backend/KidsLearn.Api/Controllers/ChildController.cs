using System.Security.Claims;
using MediatR;

public static class ChildController
{
    public static RouteGroupBuilder MapChildController(this RouteGroupBuilder apiV1, IEndpointRouteBuilder app)
    {
        var childApi = apiV1.MapGroup("/child")
            .RequireAuthorization("ChildOnly");

        childApi.MapChildFriendsEndpoints(app);

        childApi.MapGet("/assignments", async (AppDbContext db, ISender sender, ClaimsPrincipal user) =>
        {
            var childId = await ApiEndpointHelpers.ResolveChildIdAsync(db, user);
            if (!childId.HasValue)
            {
                return Results.Unauthorized();
            }

            var assignments = await sender.Send(new GetChildAssignmentsQuery(childId.Value));
            return Results.Ok(assignments);
        });

        childApi.MapGet("/assignments/{assignmentId:guid}/for-solving", async (AppDbContext db, ISender sender, ClaimsPrincipal user, Guid assignmentId) =>
        {
            var childId = await ApiEndpointHelpers.ResolveChildIdAsync(db, user);
            if (!childId.HasValue)
            {
                return Results.Unauthorized();
            }

            var result = await sender.Send(new GetChildAssignmentForSolvingQuery(childId.Value, assignmentId));
            return result.StatusCode switch
            {
                StatusCodes.Status200OK when result.Response is not null => Results.Ok(result.Response),
                StatusCodes.Status400BadRequest => Results.BadRequest(new { error = result.Error ?? "Bad request." }),
                StatusCodes.Status404NotFound => Results.NotFound(new { error = result.Error ?? "Not found." }),
                _ => Results.Problem(result.Error ?? "Unexpected error.")
            };
        });

        childApi.MapPost("/assignments/{assignmentId:guid}/answers", async (AppDbContext db, ISender sender, ClaimsPrincipal user, Guid assignmentId, SubmitAssignmentAnswersRequest request) =>
        {
            var childId = await ApiEndpointHelpers.ResolveChildIdAsync(db, user);
            if (!childId.HasValue)
            {
                return Results.Unauthorized();
            }

            var result = await sender.Send(new SubmitChildAssignmentAnswersCommand(childId.Value, assignmentId, request));
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

        childApi.MapPost("/assignments/{assignmentId:guid}/complete", async (AppDbContext db, ISender sender, ClaimsPrincipal user, Guid assignmentId) =>
        {
            var childId = await ApiEndpointHelpers.ResolveChildIdAsync(db, user);
            if (!childId.HasValue)
            {
                return Results.Unauthorized();
            }

            var result = await sender.Send(new CompleteChildAssignmentCommand(childId.Value, assignmentId));
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

        childApi.MapPost("/self-assign", async (AppDbContext db, ISender sender, ClaimsPrincipal user, SelfAssignRequest request) =>
        {
            var childId = await ApiEndpointHelpers.ResolveChildIdAsync(db, user);
            if (!childId.HasValue)
            {
                return Results.Unauthorized();
            }

            var result = await sender.Send(new SelfAssignLessonCommand(childId.Value, request.LessonId));
            return result.StatusCode switch
            {
                StatusCodes.Status200OK when result.Assignment is not null => Results.Ok(result.Assignment),
                StatusCodes.Status201Created when result.Assignment is not null => Results.Created($"/api/v1/child/assignments/{result.Assignment.Id}/for-solving", result.Assignment),
                StatusCodes.Status404NotFound => Results.NotFound(new { error = result.Error ?? "Not found." }),
                _ => Results.Problem(result.Error ?? "Unexpected error.")
            };
        });

        childApi.MapGet("/results", async (AppDbContext db, ISender sender, ClaimsPrincipal user) =>
        {
            var childId = await ApiEndpointHelpers.ResolveChildIdAsync(db, user);
            if (!childId.HasValue)
            {
                return Results.Unauthorized();
            }

            var results = await sender.Send(new GetChildResultsQuery(childId.Value));
            return Results.Ok(results);
        });

        childApi.MapGet("/results/{resultId:guid}", async (AppDbContext db, ISender sender, ClaimsPrincipal user, Guid resultId) =>
        {
            var childId = await ApiEndpointHelpers.ResolveChildIdAsync(db, user);
            if (!childId.HasValue)
            {
                return Results.Unauthorized();
            }

            var result = await sender.Send(new GetChildResultDetailQuery(childId.Value, resultId));
            return result.StatusCode switch
            {
                StatusCodes.Status200OK when result.Response is not null => Results.Ok(result.Response),
                StatusCodes.Status400BadRequest => Results.BadRequest(new { error = result.Error ?? "Bad request." }),
                StatusCodes.Status404NotFound => Results.NotFound(new { error = result.Error ?? "Not found." }),
                _ => Results.Problem(result.Error ?? "Unexpected error.")
            };
        });

        return apiV1;
    }
}
