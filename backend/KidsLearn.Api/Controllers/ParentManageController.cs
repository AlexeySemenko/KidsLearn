using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

public static class ParentManageController
{
    public static RouteGroupBuilder MapParentManageEndpoints(this RouteGroupBuilder parentApi)
    {
        parentApi.MapGet("/manage/linked-parents", async (AppDbContext db, ClaimsPrincipal user) =>
        {
            if (!ApiEndpointHelpers.TryResolveUserId(user, out var parentId))
            {
                return Results.Unauthorized();
            }

            var links = await db.ParentAccountLinks
                .AsNoTracking()
                .Where(x => x.ParentAId == parentId || x.ParentBId == parentId)
                .Select(x => new
                {
                    LinkedParentId = x.ParentAId == parentId ? x.ParentBId : x.ParentAId,
                    x.CreatedAt
                })
                .ToListAsync();

            var linkedParentIds = links.Select(x => x.LinkedParentId).Distinct().ToList();
            var usersById = await db.Users
                .AsNoTracking()
                .Where(x => linkedParentIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.Email);

            var response = links
                .Where(x => usersById.ContainsKey(x.LinkedParentId))
                .OrderBy(x => usersById[x.LinkedParentId])
                .Select(x => new LinkedParentResponse(x.LinkedParentId, usersById[x.LinkedParentId], x.CreatedAt))
                .ToList();

            return Results.Ok(response);
        });

        parentApi.MapPost("/manage/linked-parents", async (AppDbContext db, ClaimsPrincipal user, LinkParentAccountRequest request) =>
        {
            if (!ApiEndpointHelpers.TryResolveUserId(user, out var parentId))
            {
                return Results.Unauthorized();
            }

            if (string.IsNullOrWhiteSpace(request.Email))
            {
                return Results.BadRequest(new { error = "Parent email is required." });
            }

            var normalizedEmail = request.Email.Trim().ToLowerInvariant();
            var parentUser = await db.Users
                .FirstOrDefaultAsync(x => x.Id == parentId && x.Role == UserRole.Parent);

            if (parentUser is null)
            {
                return Results.NotFound(new { error = "Current parent account was not found." });
            }

            if (string.Equals(parentUser.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase))
            {
                return Results.BadRequest(new { error = "You cannot link your own parent account." });
            }

            var otherParent = await db.Users
                .FirstOrDefaultAsync(x => x.Email == normalizedEmail && x.Role == UserRole.Parent);

            if (otherParent is null)
            {
                return Results.NotFound(new { error = "Parent with this email was not found." });
            }

            var (parentAId, parentBId) = ApiEndpointHelpers.NormalizeParentLinkPair(parentId, otherParent.Id);
            var exists = await db.ParentAccountLinks.AnyAsync(x => x.ParentAId == parentAId && x.ParentBId == parentBId);
            if (exists)
            {
                return Results.BadRequest(new { error = "These parent accounts are already linked." });
            }

            var link = new ParentAccountLink
            {
                ParentAId = parentAId,
                ParentBId = parentBId,
                CreatedAt = DateTime.UtcNow
            };

            db.ParentAccountLinks.Add(link);
            await db.SaveChangesAsync();

            var response = new LinkedParentResponse(otherParent.Id, otherParent.Email, link.CreatedAt);
            return Results.Created($"/api/v1/manage/linked-parents/{otherParent.Id}", response);
        });

        parentApi.MapDelete("/manage/linked-parents/{linkedParentId:guid}", async (AppDbContext db, ClaimsPrincipal user, Guid linkedParentId) =>
        {
            if (!ApiEndpointHelpers.TryResolveUserId(user, out var parentId))
            {
                return Results.Unauthorized();
            }

            if (linkedParentId == Guid.Empty || linkedParentId == parentId)
            {
                return Results.BadRequest(new { error = "Invalid linked parent id." });
            }

            var (parentAId, parentBId) = ApiEndpointHelpers.NormalizeParentLinkPair(parentId, linkedParentId);
            var link = await db.ParentAccountLinks
                .FirstOrDefaultAsync(x => x.ParentAId == parentAId && x.ParentBId == parentBId);

            if (link is null)
            {
                return Results.NotFound(new { error = "Linked parent relation not found." });
            }

            db.ParentAccountLinks.Remove(link);
            await db.SaveChangesAsync();

            return Results.NoContent();
        });

        return parentApi;
    }
}
