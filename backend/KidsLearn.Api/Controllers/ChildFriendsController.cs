using System.Security.Claims;
using MediatR;

public static class ChildFriendsController
{
    public static RouteGroupBuilder MapChildFriendsEndpoints(this RouteGroupBuilder childApi, IEndpointRouteBuilder app)
    {
        // Public: get invite info by token (no auth required)
        app.MapGet("/api/v1/child/friends/invite/{token}", async (ISender sender, string token) =>
        {
            var result = await sender.Send(new GetChildFriendInviteQuery(token));
            return result.StatusCode switch
            {
                StatusCodes.Status200OK when result.Response is not null => Results.Ok(result.Response),
                StatusCodes.Status404NotFound => Results.NotFound(new { error = result.Error ?? "Not found." }),
                _ => Results.Problem(result.Error ?? "Unexpected error.")
            };
        });

        var friendsApi = childApi.MapGroup("/friends");

        friendsApi.MapGet("/", async (AppDbContext db, ISender sender, ClaimsPrincipal user) =>
        {
            var childId = await ApiEndpointHelpers.ResolveChildIdAsync(db, user);
            if (!childId.HasValue) return Results.Unauthorized();

            var friends = await sender.Send(new GetChildFriendsQuery(childId.Value));
            return Results.Ok(friends);
        });

        friendsApi.MapPost("/invite", async (AppDbContext db, ISender sender, ClaimsPrincipal user, SendFriendInviteRequest request, HttpContext httpContext) =>
        {
            var childId = await ApiEndpointHelpers.ResolveChildIdAsync(db, user);
            if (!childId.HasValue) return Results.Unauthorized();

            if (string.IsNullOrWhiteSpace(request.Email))
                return Results.BadRequest(new { error = "Email is required." });

            var baseUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}";
            var result = await sender.Send(new SendChildFriendInviteCommand(childId.Value, request.Email, baseUrl));

            return result.StatusCode switch
            {
                StatusCodes.Status200OK => Results.Ok(new { message = "Invitation sent." }),
                StatusCodes.Status400BadRequest => Results.BadRequest(new { error = result.Error ?? "Bad request." }),
                StatusCodes.Status404NotFound => Results.NotFound(new { error = result.Error ?? "Not found." }),
                StatusCodes.Status409Conflict => Results.Conflict(new { error = result.Error ?? "Conflict." }),
                _ => Results.Problem(result.Error ?? "Unexpected error.")
            };
        });

        friendsApi.MapPost("/invite/{token}/accept", async (AppDbContext db, ISender sender, ClaimsPrincipal user, string token) =>
        {
            var childId = await ApiEndpointHelpers.ResolveChildIdAsync(db, user);
            if (!childId.HasValue) return Results.Unauthorized();

            var result = await sender.Send(new AcceptChildFriendInviteCommand(childId.Value, token));

            return result.StatusCode switch
            {
                StatusCodes.Status200OK when result.Friend is not null => Results.Ok(result.Friend),
                StatusCodes.Status400BadRequest => Results.BadRequest(new { error = result.Error ?? "Bad request." }),
                StatusCodes.Status404NotFound => Results.NotFound(new { error = result.Error ?? "Not found." }),
                StatusCodes.Status409Conflict => Results.Conflict(new { error = result.Error ?? "Conflict." }),
                _ => Results.Problem(result.Error ?? "Unexpected error.")
            };
        });

        friendsApi.MapGet("/{friendChildId:guid}/results", async (AppDbContext db, ISender sender, ClaimsPrincipal user, Guid friendChildId) =>
        {
            var childId = await ApiEndpointHelpers.ResolveChildIdAsync(db, user);
            if (!childId.HasValue) return Results.Unauthorized();

            var result = await sender.Send(new GetFriendResultsQuery(childId.Value, friendChildId));

            return result.StatusCode switch
            {
                StatusCodes.Status200OK when result.Results is not null => Results.Ok(result.Results),
                StatusCodes.Status403Forbidden => Results.Forbid(),
                _ => Results.Problem(result.Error ?? "Unexpected error.")
            };
        });

        friendsApi.MapGet("/{friendChildId:guid}/note", async (AppDbContext db, ISender sender, ClaimsPrincipal user, Guid friendChildId) =>
        {
            var childId = await ApiEndpointHelpers.ResolveChildIdAsync(db, user);
            if (!childId.HasValue) return Results.Unauthorized();

            var result = await sender.Send(new GetFriendNoteQuery(childId.Value, friendChildId));

            return result.StatusCode switch
            {
                StatusCodes.Status200OK => Results.Ok(new FriendNoteResponse(result.MyNote)),
                StatusCodes.Status403Forbidden => Results.Forbid(),
                _ => Results.Problem(result.Error ?? "Unexpected error.")
            };
        });

        friendsApi.MapPut("/{friendChildId:guid}/note", async (AppDbContext db, ISender sender, ClaimsPrincipal user, Guid friendChildId, UpdateFriendNoteRequest request) =>
        {
            var childId = await ApiEndpointHelpers.ResolveChildIdAsync(db, user);
            if (!childId.HasValue) return Results.Unauthorized();

            var status = await sender.Send(new UpdateFriendNoteCommand(childId.Value, friendChildId, request.Note));

            return status switch
            {
                StatusCodes.Status204NoContent => Results.NoContent(),
                StatusCodes.Status403Forbidden => Results.Forbid(),
                _ => Results.Problem("Unexpected error.")
            };
        });

        return childApi;
    }
}
