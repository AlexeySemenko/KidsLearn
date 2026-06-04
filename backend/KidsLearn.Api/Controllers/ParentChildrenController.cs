using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

public static class ParentChildrenController
{
    public static RouteGroupBuilder MapParentChildrenEndpoints(this RouteGroupBuilder parentApi)
    {
        parentApi.MapGet("/children", async (AppDbContext db, ClaimsPrincipal user) =>
        {
            if (!ApiEndpointHelpers.TryResolveUserId(user, out var parentId))
            {
                return Results.Unauthorized();
            }

            var children = await db.Children
                .Where(x => x.ParentId == parentId)
                .Select(x => new ChildResponse(x.Id, x.ParentId, x.Name, x.Grade))
                .ToListAsync();

            return Results.Ok(children);
        });

        parentApi.MapPost("/children", async (AppDbContext db, ClaimsPrincipal user, IPasswordHasherService passwordHasher, CreateChildRequest request) =>
        {
            if (!ApiEndpointHelpers.TryResolveUserId(user, out var parentId))
            {
                return Results.Unauthorized();
            }

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Results.BadRequest(new { error = "Name is required." });
            }

            if (request.Grade is < 1 or > 12)
            {
                return Results.BadRequest(new { error = "Grade must be between 1 and 12." });
            }

            var parentExists = await db.Users.AnyAsync(x => x.Id == parentId && x.Role == UserRole.Parent);
            if (!parentExists)
            {
                return Results.NotFound(new { error = "Parent was not found." });
            }

            var accessCode = string.IsNullOrWhiteSpace(request.AccessCode)
                ? ApiEndpointHelpers.GenerateAccessCode()
                : request.AccessCode.Trim();

            if (accessCode.Length < 4)
            {
                return Results.BadRequest(new { error = "Access code must contain at least 4 characters." });
            }

            var childUser = new AppUser
            {
                Email = $"child-{Guid.NewGuid():N}@kidslearn.local",
                PasswordHash = passwordHasher.HashPassword(accessCode),
                Role = UserRole.Child,
                CreatedAt = DateTime.UtcNow
            };

            var child = new Child
            {
                ParentId = parentId,
                User = childUser,
                Name = request.Name.Trim(),
                Grade = request.Grade
            };

            db.Children.Add(child);
            await db.SaveChangesAsync();

            return Results.Created(
                $"/api/v1/children/{child.Id}",
                new CreatedChildResponse(new ChildResponse(child.Id, child.ParentId, child.Name, child.Grade), accessCode));
        });

        parentApi.MapPatch("/children/{childId:guid}", async (AppDbContext db, ClaimsPrincipal user, IPasswordHasherService passwordHasher, Guid childId, UpdateChildRequest request) =>
        {
            if (!ApiEndpointHelpers.TryResolveUserId(user, out var parentId))
            {
                return Results.Unauthorized();
            }

            var child = await db.Children.FirstOrDefaultAsync(x => x.Id == childId && x.ParentId == parentId);
            if (child is null)
            {
                return Results.NotFound();
            }

            if (request.Name is not null)
            {
                if (string.IsNullOrWhiteSpace(request.Name))
                {
                    return Results.BadRequest(new { error = "Name cannot be empty." });
                }

                child.Name = request.Name.Trim();
            }

            if (request.Grade.HasValue)
            {
                if (request.Grade.Value is < 1 or > 12)
                {
                    return Results.BadRequest(new { error = "Grade must be between 1 and 12." });
                }

                child.Grade = request.Grade.Value;
            }

            if (request.AccessCode is not null)
            {
                if (string.IsNullOrWhiteSpace(request.AccessCode) || request.AccessCode.Trim().Length < 4)
                {
                    return Results.BadRequest(new { error = "Access code must contain at least 4 characters." });
                }

                if (child.UserId.HasValue)
                {
                    var childUser = await db.Users.FirstOrDefaultAsync(x => x.Id == child.UserId.Value && x.Role == UserRole.Child);
                    if (childUser is not null)
                    {
                        childUser.PasswordHash = passwordHasher.HashPassword(request.AccessCode.Trim());
                    }
                }
            }

            await db.SaveChangesAsync();
            return Results.Ok(new ChildResponse(child.Id, child.ParentId, child.Name, child.Grade));
        });

        parentApi.MapPost("/children/{childId:guid}/access-code/reset", async (AppDbContext db, ClaimsPrincipal user, IPasswordHasherService passwordHasher, Guid childId) =>
        {
            if (!ApiEndpointHelpers.TryResolveUserId(user, out var parentId))
            {
                return Results.Unauthorized();
            }

            var child = await db.Children.FirstOrDefaultAsync(x => x.Id == childId && x.ParentId == parentId);
            if (child is null || !child.UserId.HasValue)
            {
                return Results.NotFound();
            }

            var childUser = await db.Users.FirstOrDefaultAsync(x => x.Id == child.UserId.Value && x.Role == UserRole.Child);
            if (childUser is null)
            {
                return Results.NotFound();
            }

            var newCode = ApiEndpointHelpers.GenerateAccessCode();
            childUser.PasswordHash = passwordHasher.HashPassword(newCode);
            await db.SaveChangesAsync();

            return Results.Ok(new ResetChildAccessCodeResponse(child.Id, newCode));
        });

        parentApi.MapDelete("/children/{childId:guid}", async (AppDbContext db, ClaimsPrincipal user, Guid childId) =>
        {
            if (!ApiEndpointHelpers.TryResolveUserId(user, out var parentId))
            {
                return Results.Unauthorized();
            }

            var child = await db.Children.FirstOrDefaultAsync(x => x.Id == childId && x.ParentId == parentId);
            if (child is null)
            {
                return Results.NotFound();
            }

            AppUser? childUser = null;
            if (child.UserId.HasValue)
            {
                childUser = await db.Users.FirstOrDefaultAsync(x => x.Id == child.UserId.Value && x.Role == UserRole.Child);
            }

            db.Children.Remove(child);
            if (childUser is not null)
            {
                db.Users.Remove(childUser);
            }

            await db.SaveChangesAsync();

            return Results.NoContent();
        });

        return parentApi;
    }
}


