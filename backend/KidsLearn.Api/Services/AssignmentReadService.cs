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

        var query = db.Assignments
            .AsNoTracking()
            .Where(x => scopedParentIds.Contains(x.Child.ParentId));

        if (childId.HasValue)
        {
            query = query.Where(x => x.ChildId == childId.Value);
        }

        return await query
            .OrderByDescending(x => x.AssignedAt)
            .Select(x => new AssignmentResponse(
                x.Id,
                x.ChildId,
                x.LessonId,
                x.Lesson.Title,
                x.AssignedAt,
                x.DueDate,
                x.Status))
            .ToListAsync();
    }

    public async Task<IReadOnlyList<AssignmentResponse>> ListForChildAsync(Guid childId)
    {
        return await db.Assignments
            .AsNoTracking()
            .Where(x => x.ChildId == childId)
            .OrderByDescending(x => x.AssignedAt)
            .Select(x => new AssignmentResponse(
                x.Id,
                x.ChildId,
                x.LessonId,
                x.Lesson.Title,
                x.AssignedAt,
                x.DueDate,
                x.Status))
            .ToListAsync();
    }
}
