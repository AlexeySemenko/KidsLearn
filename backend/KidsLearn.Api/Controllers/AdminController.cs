using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using MediatR;

public static class AdminController
{
    public static RouteGroupBuilder MapAdminController(this RouteGroupBuilder apiV1)
    {
        var admin = apiV1.MapGroup("/admin").RequireAuthorization("AdminOnly");

        admin.MapGet("/users", async (ISender sender) =>
        {
            var users = await sender.Send(new GetAdminUsersQuery());
            return Results.Ok(users);
        });

        admin.MapPost("/users", async (ISender sender, ClaimsPrincipal caller, AdminCreateUserRequest request) =>
        {
            var callerEmail = caller.FindFirstValue(JwtRegisteredClaimNames.Email) ?? "KidsLearnAI Admin";
            var result = await sender.Send(new CreateAdminUserCommand(request, callerEmail));

            return result.StatusCode switch
            {
                StatusCodes.Status201Created => Results.Created(
                    $"/api/v1/admin/users/{result.User!.Id}",
                    new AdminCreateUserResponse(result.User, result.EmailSent)),
                StatusCodes.Status409Conflict => Results.Conflict(new { error = result.Error }),
                _ => Results.BadRequest(new { error = result.Error }),
            };
        });

        admin.MapPatch("/users/{userId:guid}", async (ISender sender, Guid userId, AdminUpdateUserRequest request) =>
        {
            var result = await sender.Send(new UpdateAdminUserCommand(userId, request));

            return result.StatusCode switch
            {
                StatusCodes.Status200OK => Results.Ok(result.User),
                StatusCodes.Status404NotFound => Results.NotFound(new { error = result.Error }),
                _ => Results.BadRequest(new { error = result.Error }),
            };
        });

        admin.MapDelete("/users/{userId:guid}", async (ISender sender, Guid userId, ClaimsPrincipal caller) =>
        {
            var callerId = ApiEndpointHelpers.ResolveUserId(caller) ?? Guid.Empty;
            var result = await sender.Send(new DeleteAdminUserCommand(userId, callerId));

            return result.StatusCode switch
            {
                StatusCodes.Status204NoContent => Results.NoContent(),
                StatusCodes.Status404NotFound => Results.NotFound(new { error = result.Error }),
                _ => Results.BadRequest(new { error = result.Error }),
            };
        });

        return apiV1;
    }
}
