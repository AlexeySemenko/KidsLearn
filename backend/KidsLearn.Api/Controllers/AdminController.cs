using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

public static class AdminController
{
    public static RouteGroupBuilder MapAdminController(this RouteGroupBuilder apiV1)
    {
        var admin = apiV1.MapGroup("/admin").RequireAuthorization("AdminOnly");

        admin.MapGet("/users", async (AppDbContext db) =>
        {
            var users = await db.Users
                .AsNoTracking()
                .OrderBy(u => u.CreatedAt)
                .Select(u => new AdminUserResponse(
                    u.Id,
                    u.Email,
                    u.DisplayName,
                    u.Role.ToString(),
                    u.EmailVerified,
                    u.ExternalProvider,
                    u.CreatedAt,
                    u.LastAccessAt))
                .ToListAsync();

            return Results.Ok(users);
        });

        admin.MapPost("/users", async (
            AppDbContext db,
            IEmailService emailService,
            ClaimsPrincipal caller,
            AdminCreateUserRequest request) =>
        {
            if (string.IsNullOrWhiteSpace(request.Email))
            {
                return Results.BadRequest(new { error = "Email is required." });
            }

            var normalizedEmail = request.Email.Trim().ToLowerInvariant();

            if (!Enum.TryParse<UserRole>(request.Role, ignoreCase: true, out var role))
            {
                return Results.BadRequest(new { error = $"Invalid role '{request.Role}'. Valid values: Parent, Child, Admin." });
            }

            var existing = await db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail);
            if (existing is not null)
            {
                return Results.Conflict(new { error = "A user with this email already exists." });
            }

            var user = new AppUser
            {
                Email = normalizedEmail,
                DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? null : request.DisplayName.Trim(),
                PasswordHash = string.Empty,
                Role = role,
                EmailVerified = false,
                CreatedAt = DateTime.UtcNow,
            };

            db.Users.Add(user);
            await db.SaveChangesAsync();

            var callerEmail = caller.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email) ?? "KidsLearnAI Admin";
            var emailSent = await emailService.SendInvitationAsync(user.Email, user.DisplayName, callerEmail);

            var userResponse = new AdminUserResponse(
                user.Id,
                user.Email,
                user.DisplayName,
                user.Role.ToString(),
                user.EmailVerified,
                user.ExternalProvider,
                user.CreatedAt,
                user.LastAccessAt);

            return Results.Created($"/api/v1/admin/users/{user.Id}", new AdminCreateUserResponse(userResponse, emailSent));
        });

        admin.MapPatch("/users/{userId:guid}", async (
            AppDbContext db,
            Guid userId,
            AdminUpdateUserRequest request) =>
        {
            var user = await db.Users.FindAsync(userId);
            if (user is null)
            {
                return Results.NotFound(new { error = "User not found." });
            }

            if (request.DisplayName is not null)
            {
                user.DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? null : request.DisplayName.Trim();
            }

            if (!string.IsNullOrWhiteSpace(request.Role))
            {
                if (!Enum.TryParse<UserRole>(request.Role, ignoreCase: true, out var newRole))
                {
                    return Results.BadRequest(new { error = $"Invalid role '{request.Role}'." });
                }
                user.Role = newRole;
            }

            await db.SaveChangesAsync();

            return Results.Ok(new AdminUserResponse(
                user.Id,
                user.Email,
                user.DisplayName,
                user.Role.ToString(),
                user.EmailVerified,
                user.ExternalProvider,
                user.CreatedAt,
                user.LastAccessAt));
        });

        admin.MapDelete("/users/{userId:guid}", async (AppDbContext db, Guid userId, ClaimsPrincipal caller) =>
        {
            var callerId = ApiEndpointHelpers.ResolveUserId(caller);
            if (callerId == userId)
            {
                return Results.BadRequest(new { error = "You cannot delete your own account." });
            }

            var user = await db.Users.FindAsync(userId);
            if (user is null)
            {
                return Results.NotFound(new { error = "User not found." });
            }

            db.Users.Remove(user);
            await db.SaveChangesAsync();

            return Results.NoContent();
        });

        return apiV1;
    }
}
