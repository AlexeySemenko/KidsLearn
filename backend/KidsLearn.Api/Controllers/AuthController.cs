using MediatR;

public static class AuthController
{
    public static RouteGroupBuilder MapAuthController(this RouteGroupBuilder apiV1)
    {
        apiV1.MapPost("/auth/register", async (ISender sender, RegisterRequest request) =>
        {
            var result = await sender.Send(new RegisterParentCommand(request));
            return result.StatusCode switch
            {
                StatusCodes.Status201Created when result.User is not null
                    => Results.Created($"/api/v1/users/{result.User.Id}", result.User),
                StatusCodes.Status400BadRequest
                    => Results.BadRequest(new { error = result.Error ?? "Bad request." }),
                StatusCodes.Status409Conflict
                    => Results.Conflict(new { error = result.Error ?? "Conflict." }),
                _ => Results.Problem(result.Error ?? "Unexpected error.")
            };
        });

        apiV1.MapPost("/auth/login", async (ISender sender, LoginRequest request) =>
        {
            var result = await sender.Send(new LoginParentCommand(request));
            return result.StatusCode switch
            {
                StatusCodes.Status200OK when result.Response is not null => Results.Ok(result.Response),
                StatusCodes.Status400BadRequest => Results.BadRequest(new { error = result.Error ?? "Bad request." }),
                StatusCodes.Status401Unauthorized => Results.Unauthorized(),
                _ => Results.Problem(result.Error ?? "Unexpected error.")
            };
        });

        apiV1.MapPost("/auth/child-login", async (ISender sender, ChildLoginRequest request) =>
        {
            var result = await sender.Send(new LoginChildCommand(request));
            return result.StatusCode switch
            {
                StatusCodes.Status200OK when result.Response is not null => Results.Ok(result.Response),
                StatusCodes.Status400BadRequest => Results.BadRequest(new { error = result.Error ?? "Bad request." }),
                StatusCodes.Status401Unauthorized => Results.Unauthorized(),
                _ => Results.Problem(result.Error ?? "Unexpected error.")
            };
        });

        apiV1.MapPost("/auth/refresh", async (ISender sender, RefreshRequest request) =>
        {
            var result = await sender.Send(new RefreshAuthTokenCommand(request));
            return result.StatusCode switch
            {
                StatusCodes.Status200OK when result.Response is not null => Results.Ok(result.Response),
                StatusCodes.Status400BadRequest => Results.BadRequest(new { error = result.Error ?? "Bad request." }),
                StatusCodes.Status401Unauthorized => Results.Unauthorized(),
                _ => Results.Problem(result.Error ?? "Unexpected error.")
            };
        });

        apiV1.MapPost("/auth/revoke", async (ISender sender, RevokeRequest request) =>
        {
            var result = await sender.Send(new RevokeAuthTokenCommand(request));
            return result.StatusCode switch
            {
                StatusCodes.Status204NoContent => Results.NoContent(),
                StatusCodes.Status400BadRequest => Results.BadRequest(new { error = result.Error ?? "Bad request." }),
                StatusCodes.Status404NotFound => Results.NotFound(),
                _ => Results.Problem(result.Error ?? "Unexpected error.")
            };
        });

        return apiV1;
    }
}
