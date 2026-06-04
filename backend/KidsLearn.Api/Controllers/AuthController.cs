using Microsoft.EntityFrameworkCore;

public static class AuthController
{
    public static RouteGroupBuilder MapAuthController(this RouteGroupBuilder apiV1, IConfiguration configuration)
    {
        apiV1.MapPost("/auth/register", async (AppDbContext db, IPasswordHasherService passwordHasher, RegisterRequest request) =>
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            {
                return Results.BadRequest(new { error = "Email and password are required." });
            }

            var email = request.Email.Trim().ToLowerInvariant();
            if (!email.Contains('@'))
            {
                return Results.BadRequest(new { error = "Email format is invalid." });
            }

            if (request.Password.Length < 8)
            {
                return Results.BadRequest(new { error = "Password must be at least 8 characters." });
            }

            var exists = await db.Users.AnyAsync(x => x.Email == email);
            if (exists)
            {
                return Results.Conflict(new { error = "User with this email already exists." });
            }

            var user = new AppUser
            {
                Email = email,
                PasswordHash = passwordHasher.HashPassword(request.Password),
                Role = UserRole.Parent,
                CreatedAt = DateTime.UtcNow
            };

            db.Users.Add(user);
            await db.SaveChangesAsync();

            return Results.Created($"/api/v1/users/{user.Id}", new AuthUserResponse(user.Id, user.Email, user.Role.ToString()));
        });

        apiV1.MapPost("/auth/login", async (AppDbContext db, IPasswordHasherService passwordHasher, IJwtTokenService tokenService, LoginRequest request) =>
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            {
                return Results.BadRequest(new { error = "Email and password are required." });
            }

            var email = request.Email.Trim().ToLowerInvariant();
            var user = await db.Users.FirstOrDefaultAsync(x => x.Email == email);
            if (user is null || string.IsNullOrWhiteSpace(user.PasswordHash) || !passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
            {
                return Results.Unauthorized();
            }

            var accessToken = tokenService.CreateAccessToken(user);
            var refreshToken = tokenService.CreateRefreshToken();
            var refreshExpiresDays = int.TryParse(configuration["Jwt:RefreshTokenExpirationDays"], out var refreshDays)
                ? refreshDays
                : 14;

            db.RefreshTokens.Add(new RefreshToken
            {
                UserId = user.Id,
                Token = refreshToken,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(refreshExpiresDays)
            });

            await db.SaveChangesAsync();

            var expiresIn = int.TryParse(configuration["Jwt:AccessTokenExpirationMinutes"], out var accessMinutes)
                ? accessMinutes * 60
                : 1800;

            return Results.Ok(new AuthTokenResponse(
                accessToken,
                refreshToken,
                expiresIn,
                new AuthUserResponse(user.Id, user.Email, user.Role.ToString())));
        });

        apiV1.MapPost("/auth/child-login", async (AppDbContext db, IPasswordHasherService passwordHasher, IJwtTokenService tokenService, ChildLoginRequest request) =>
        {
            if (request.ChildId == Guid.Empty || string.IsNullOrWhiteSpace(request.AccessCode))
            {
                return Results.BadRequest(new { error = "ChildId and access code are required." });
            }

            var child = await db.Children
                .Include(x => x.User)
                .FirstOrDefaultAsync(x => x.Id == request.ChildId);

            if (child?.User is null || child.User.Role != UserRole.Child)
            {
                return Results.Unauthorized();
            }

            if (string.IsNullOrWhiteSpace(child.User.PasswordHash)
                || !passwordHasher.VerifyPassword(request.AccessCode.Trim(), child.User.PasswordHash))
            {
                return Results.Unauthorized();
            }

            var accessToken = tokenService.CreateAccessToken(child.User);
            var refreshToken = tokenService.CreateRefreshToken();
            var refreshExpiresDays = int.TryParse(configuration["Jwt:RefreshTokenExpirationDays"], out var refreshDays)
                ? refreshDays
                : 14;

            db.RefreshTokens.Add(new RefreshToken
            {
                UserId = child.User.Id,
                Token = refreshToken,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(refreshExpiresDays)
            });

            await db.SaveChangesAsync();

            var expiresIn = int.TryParse(configuration["Jwt:AccessTokenExpirationMinutes"], out var accessMinutes)
                ? accessMinutes * 60
                : 1800;

            return Results.Ok(new AuthTokenResponse(
                accessToken,
                refreshToken,
                expiresIn,
                new AuthUserResponse(child.User.Id, child.Name, child.User.Role.ToString())));
        });

        apiV1.MapPost("/auth/refresh", async (AppDbContext db, IJwtTokenService tokenService, RefreshRequest request) =>
        {
            if (string.IsNullOrWhiteSpace(request.RefreshToken))
            {
                return Results.BadRequest(new { error = "Refresh token is required." });
            }

            var existing = await db.RefreshTokens
                .Include(x => x.User)
                .FirstOrDefaultAsync(x => x.Token == request.RefreshToken);

            if (existing is null || existing.RevokedAt.HasValue || existing.ExpiresAt <= DateTime.UtcNow)
            {
                return Results.Unauthorized();
            }

            existing.RevokedAt = DateTime.UtcNow;

            var newRefreshToken = tokenService.CreateRefreshToken();
            var refreshExpiresDays = int.TryParse(configuration["Jwt:RefreshTokenExpirationDays"], out var refreshDays)
                ? refreshDays
                : 14;

            db.RefreshTokens.Add(new RefreshToken
            {
                UserId = existing.UserId,
                Token = newRefreshToken,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(refreshExpiresDays)
            });

            var accessToken = tokenService.CreateAccessToken(existing.User);
            await db.SaveChangesAsync();

            var expiresIn = int.TryParse(configuration["Jwt:AccessTokenExpirationMinutes"], out var accessMinutes)
                ? accessMinutes * 60
                : 1800;

            return Results.Ok(new AuthTokenResponse(
                accessToken,
                newRefreshToken,
                expiresIn,
                new AuthUserResponse(existing.User.Id, existing.User.Email, existing.User.Role.ToString())));
        });

        apiV1.MapPost("/auth/revoke", async (AppDbContext db, RevokeRequest request) =>
        {
            if (string.IsNullOrWhiteSpace(request.RefreshToken))
            {
                return Results.BadRequest(new { error = "Refresh token is required." });
            }

            var existing = await db.RefreshTokens.FirstOrDefaultAsync(x => x.Token == request.RefreshToken);
            if (existing is null)
            {
                return Results.NotFound();
            }

            if (!existing.RevokedAt.HasValue)
            {
                existing.RevokedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
            }

            return Results.NoContent();
        });

        return apiV1;
    }
}
