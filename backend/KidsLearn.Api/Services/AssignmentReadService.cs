using Microsoft.EntityFrameworkCore;

public interface IAssignmentReadService
{
    Task<IReadOnlyList<AssignmentResponse>> ListForParentAsync(Guid parentId, Guid? childId);
    Task<IReadOnlyList<AssignmentResponse>> ListForChildAsync(Guid childId);
}

public sealed class AssignmentReadService(AppDbContext db) : IAssignmentReadService
{
    public async Task<IReadOnlyList<AssignmentResponse>> ListForParentAsync(Guid parentId, Guid? childId)
    {
        var scopedParentIds = await ApiEndpointHelpers.ResolveParentScopeIdsAsync(db, parentId);
        var weekAgo = DateTime.UtcNow.AddDays(-7);

        var query = db.Assignments
            .AsNoTracking()
            .Where(x => scopedParentIds.Contains(x.Child.ParentId)
                && (x.Status != "Completed" || x.AssignedAt >= weekAgo));

        if (childId.HasValue)
        {
            query = query.Where(x => x.ChildId == childId.Value);
        }

        return await query
            .OrderByDescending(x => x.AssignedAt)
            .Select(x => new AssignmentResponse(
                x.Id,
                x.ChildId,
                x.Child.Name,
                x.LessonId,
                x.Lesson.Title,
                x.Lesson.Subject,
                x.AssignedAt,
                x.DueDate,
                x.Status,
                x.Result != null ? x.Result.Id : (Guid?)null,
                x.Result != null ? x.Result.Score : (decimal?)null))
            .ToListAsync();
    }

    public async Task<IReadOnlyList<AssignmentResponse>> ListForChildAsync(Guid childId)
    {
        var weekAgo = DateTime.UtcNow.AddDays(-7);

        return await db.Assignments
            .AsNoTracking()
            .Where(x => x.ChildId == childId && x.AssignedAt >= weekAgo)
            .OrderByDescending(x => x.AssignedAt)
            .Select(x => new AssignmentResponse(
                x.Id,
                x.ChildId,
                x.Child.Name,
                x.LessonId,
                x.Lesson.Title,
                x.Lesson.Subject,
                x.AssignedAt,
                x.DueDate,
                x.Status,
                x.Result != null ? x.Result.Id : (Guid?)null,
                x.Result != null ? x.Result.Score : (decimal?)null))
            .ToListAsync();
    }
}
