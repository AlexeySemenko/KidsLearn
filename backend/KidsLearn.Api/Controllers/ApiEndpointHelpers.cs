using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

public static class ApiEndpointHelpers
{
    public static Guid? ResolveUserId(ClaimsPrincipal user)
    {
        var candidate = user.FindFirstValue("sub")
                        ?? user.FindFirstValue(ClaimTypes.NameIdentifier)
                        ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub);

        return Guid.TryParse(candidate, out var userId) ? userId : null;
    }

    public static bool TryResolveUserId(ClaimsPrincipal user, out Guid userId)
    {
        var resolvedUserId = ResolveUserId(user);
        if (!resolvedUserId.HasValue)
        {
            userId = Guid.Empty;
            return false;
        }

        userId = resolvedUserId.Value;
        return true;
    }

    public static bool IsGradeInRange(int grade)
    {
        return grade is >= 1 and <= 12;
    }

    public static string GenerateAccessCode()
    {
        return Random.Shared.Next(100000, 999999).ToString();
    }

    public static async Task<Guid?> ResolveChildIdAsync(AppDbContext db, ClaimsPrincipal user)
    {
        var userId = ResolveUserId(user);
        if (!userId.HasValue)
        {
            return null;
        }

        return await db.Children
            .AsNoTracking()
            .Where(x => x.UserId == userId.Value)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync();
    }

    public static async Task<bool> EnsureParentOwnsChildAsync(AppDbContext db, Guid parentId, Guid childId)
    {
        return await db.Children.AnyAsync(x => x.Id == childId && x.ParentId == parentId);
    }

}

