using System.Security.Claims;
using MediatR;

public static class ParentChildrenController
{
    public static RouteGroupBuilder MapParentChildrenEndpoints(this RouteGroupBuilder parentApi)
    {
        parentApi.MapGet("/children", async (ISender sender, ClaimsPrincipal user) =>
        {
            if (!ApiEndpointHelpers.TryResolveUserId(user, out var parentId))
            {
                return Results.Unauthorized();
            }

            var children = await sender.Send(new GetParentChildrenQuery(parentId));

            return Results.Ok(children);
        });

        parentApi.MapPost("/children", async (ISender sender, ClaimsPrincipal user, CreateChildRequest request) =>
        {
            if (!ApiEndpointHelpers.TryResolveUserId(user, out var parentId))
            {
                return Results.Unauthorized();
            }

            var result = await sender.Send(new CreateParentChildCommand(parentId, request));
            return result.StatusCode switch
            {
                StatusCodes.Status201Created when result.Response is not null
                    => Results.Created($"/api/v1/children/{result.Response.Child.Id}", result.Response),
                StatusCodes.Status400BadRequest
                    => Results.BadRequest(new { error = result.Error ?? "Bad request." }),
                StatusCodes.Status404NotFound
                    => Results.NotFound(new { error = result.Error ?? "Not found." }),
                _ => Results.Problem(result.Error ?? "Unexpected error.")
            };
        });

        parentApi.MapPost("/children/with-gmail", async (ISender sender, ClaimsPrincipal user, CreateChildWithGmailRequest request) =>
        {
            if (!ApiEndpointHelpers.TryResolveUserId(user, out var parentId))
            {
                return Results.Unauthorized();
            }

            var result = await sender.Send(new CreateParentChildWithGmailCommand(parentId, request));
            return result.StatusCode switch
            {
                StatusCodes.Status201Created when result.Response is not null
                    => Results.Created($"/api/v1/children/{result.Response.Child.Id}", result.Response),
                StatusCodes.Status400BadRequest
                    => Results.BadRequest(new { error = result.Error ?? "Bad request." }),
                StatusCodes.Status404NotFound
                    => Results.NotFound(new { error = result.Error ?? "Not found." }),
                _ => Results.Problem(result.Error ?? "Unexpected error.")
            };
        });

        parentApi.MapPatch("/children/{childId:guid}", async (ISender sender, ClaimsPrincipal user, Guid childId, UpdateChildRequest request) =>
        {
            if (!ApiEndpointHelpers.TryResolveUserId(user, out var parentId))
            {
                return Results.Unauthorized();
            }

            var result = await sender.Send(new UpdateParentChildCommand(parentId, childId, request));
            return result.StatusCode switch
            {
                StatusCodes.Status200OK when result.Response is not null => Results.Ok(result.Response),
                StatusCodes.Status400BadRequest => Results.BadRequest(new { error = result.Error ?? "Bad request." }),
                StatusCodes.Status404NotFound => Results.NotFound(),
                _ => Results.Problem(result.Error ?? "Unexpected error.")
            };
        });

        parentApi.MapPost("/children/{childId:guid}/access-code/reset", async (ISender sender, ClaimsPrincipal user, Guid childId) =>
        {
            if (!ApiEndpointHelpers.TryResolveUserId(user, out var parentId))
            {
                return Results.Unauthorized();
            }

            var result = await sender.Send(new ResetParentChildAccessCodeCommand(parentId, childId));
            return result.StatusCode switch
            {
                StatusCodes.Status200OK when result.Response is not null => Results.Ok(result.Response),
                StatusCodes.Status404NotFound => Results.NotFound(),
                _ => Results.Problem("Unexpected error.")
            };
        });

        parentApi.MapDelete("/children/{childId:guid}", async (ISender sender, ClaimsPrincipal user, Guid childId) =>
        {
            if (!ApiEndpointHelpers.TryResolveUserId(user, out var parentId))
            {
                return Results.Unauthorized();
            }

            var result = await sender.Send(new DeleteParentChildCommand(parentId, childId));
            return result.StatusCode switch
            {
                StatusCodes.Status204NoContent => Results.NoContent(),
                StatusCodes.Status404NotFound => Results.NotFound(),
                _ => Results.Problem("Unexpected error.")
            };
        });

        return parentApi;
    }
}


