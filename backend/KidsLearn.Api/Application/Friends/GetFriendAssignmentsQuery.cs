using MediatR;
using Microsoft.EntityFrameworkCore;

public sealed record GetFriendAssignmentsQuery(Guid RequestingChildId, Guid FriendChildId)
    : IRequest<GetFriendAssignmentsResult>;

public sealed record GetFriendAssignmentsResult(int StatusCode, List<FriendAssignmentResponse>? Assignments, string? Error = null);

public sealed record FriendAssignmentResponse(
    Guid AssignmentId,
    Guid LessonId,
    string LessonTitle,
    string LessonSubject,
    string Status,
    DateTime AssignedAt);

public sealed class GetFriendAssignmentsQueryHandler(AppDbContext db)
    : IRequestHandler<GetFriendAssignmentsQuery, GetFriendAssignmentsResult>
{
    public async Task<GetFriendAssignmentsResult> Handle(GetFriendAssignmentsQuery query, CancellationToken cancellationToken)
    {
        var areFriends = await db.ChildFriendships
            .AsNoTracking()
            .AnyAsync(f => f.Status == "Accepted" &&
                ((f.RequesterId == query.RequestingChildId && f.AcceptorId == query.FriendChildId) ||
                 (f.RequesterId == query.FriendChildId && f.AcceptorId == query.RequestingChildId)),
                cancellationToken);

        if (!areFriends)
            return new GetFriendAssignmentsResult(StatusCodes.Status403Forbidden, null, "Not friends.");

        var assignments = await db.Assignments
            .AsNoTracking()
            .Where(a => a.ChildId == query.FriendChildId && a.Status != "Completed")
            .OrderByDescending(a => a.AssignedAt)
            .Select(a => new FriendAssignmentResponse(
                a.Id,
                a.LessonId,
                a.Lesson.Title,
                a.Lesson.Subject,
                a.Status,
                a.AssignedAt))
            .ToListAsync(cancellationToken);

        return new GetFriendAssignmentsResult(StatusCodes.Status200OK, assignments);
    }
}
