using MediatR;
using Microsoft.EntityFrameworkCore;

public sealed record GetFriendResultsQuery(Guid RequestingChildId, Guid FriendChildId) : IRequest<GetFriendResultsResult>;
public sealed record GetFriendResultsResult(int StatusCode, List<ResultListItemResponse>? Results, string? Error = null);

public sealed class GetFriendResultsQueryHandler(AppDbContext db)
    : IRequestHandler<GetFriendResultsQuery, GetFriendResultsResult>
{
    public async Task<GetFriendResultsResult> Handle(GetFriendResultsQuery query, CancellationToken cancellationToken)
    {
        var areFriends = await db.ChildFriendships
            .AsNoTracking()
            .AnyAsync(f => f.Status == "Accepted" &&
                ((f.RequesterId == query.RequestingChildId && f.AcceptorId == query.FriendChildId) ||
                 (f.RequesterId == query.FriendChildId && f.AcceptorId == query.RequestingChildId)),
                cancellationToken);

        if (!areFriends)
            return new GetFriendResultsResult(StatusCodes.Status403Forbidden, null, "Not friends.");

        var results = await db.Results
            .AsNoTracking()
            .Include(x => x.Assignment)
                .ThenInclude(x => x.Lesson)
            .Where(x => x.Assignment.ChildId == query.FriendChildId)
            .OrderByDescending(x => x.CompletedAt)
            .Select(x => new ResultListItemResponse(
                x.Id,
                x.AssignmentId,
                x.Assignment.Lesson.Title,
                x.Assignment.Lesson.Subject,
                x.Assignment.Lesson.Topic,
                x.Assignment.Lesson.Grade,
                x.Score,
                x.CompletedAt,
                x.CorrectAnswers,
                x.TotalQuestions))
            .ToListAsync(cancellationToken);

        return new GetFriendResultsResult(StatusCodes.Status200OK, results);
    }
}
