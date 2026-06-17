using System.Security.Claims;
using MediatR;

public static class ParentManageController
{
    public static RouteGroupBuilder MapParentManageEndpoints(this RouteGroupBuilder parentApi)
    {
        parentApi.MapGet("/manage/linked-parents", async (ISender sender, ClaimsPrincipal user) =>
        {
            if (!ApiEndpointHelpers.TryResolveUserId(user, out var parentId))
                return Results.Unauthorized();

            var result = await sender.Send(new GetLinkedParentsQuery(parentId));
            return Results.Ok(result);
        });

        parentApi.MapPost("/manage/linked-parents", async (ISender sender, ClaimsPrincipal user, LinkParentAccountRequest request) =>
        {
            if (!ApiEndpointHelpers.TryResolveUserId(user, out var parentId))
                return Results.Unauthorized();

            var result = await sender.Send(new LinkParentAccountCommand(parentId, request.Email));

            return result.StatusCode switch
            {
                StatusCodes.Status201Created => Results.Created(
                    $"/api/v1/manage/linked-parents/{result.Response!.ParentId}",
                    new LinkParentAccountResponse(result.Response, result.EmailSent)),
                StatusCodes.Status404NotFound => Results.NotFound(new { error = result.Error }),
                _ => Results.BadRequest(new { error = result.Error }),
            };
        });

        parentApi.MapDelete("/manage/linked-parents/{linkedParentId:guid}", async (ISender sender, ClaimsPrincipal user, Guid linkedParentId) =>
        {
            if (!ApiEndpointHelpers.TryResolveUserId(user, out var parentId))
                return Results.Unauthorized();

            var result = await sender.Send(new UnlinkParentAccountCommand(parentId, linkedParentId));

            return result.StatusCode switch
            {
                StatusCodes.Status204NoContent => Results.NoContent(),
                StatusCodes.Status404NotFound => Results.NotFound(new { error = result.Error }),
                _ => Results.BadRequest(new { error = result.Error }),
            };
        });

        return parentApi;
    }
}
