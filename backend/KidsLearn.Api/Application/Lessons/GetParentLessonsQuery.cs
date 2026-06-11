using MediatR;
using Microsoft.EntityFrameworkCore;

public sealed record GetParentLessonsQuery(
    Guid ParentId,
    string? Subject,
    int? Grade,
    string? Topic,
    int Page,
    int PageSize) : IRequest<GetParentLessonsResult>;

public sealed record GetParentLessonsResult(
    IReadOnlyList<LessonSummaryResponse> Items,
    int Total,
    int Page,
    int PageSize);

public sealed class GetParentLessonsQueryHandler : IRequestHandler<GetParentLessonsQuery, GetParentLessonsResult>
{
    private readonly AppDbContext _db;

    public GetParentLessonsQueryHandler(AppDbContext db)
    {
        _db = db;
    }

    public async Task<GetParentLessonsResult> Handle(GetParentLessonsQuery query, CancellationToken cancellationToken)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var scopedParentIds = await ApiEndpointHelpers.ResolveParentScopeIdsAsync(_db, query.ParentId);

        var lessonsQuery = _db.Lessons
            .AsNoTracking()
            .Where(x => scopedParentIds.Contains(x.CreatedBy));

        if (!string.IsNullOrWhiteSpace(query.Subject))
        {
            lessonsQuery = lessonsQuery.Where(x => x.Subject == query.Subject.Trim());
        }

        if (query.Grade.HasValue)
        {
            lessonsQuery = lessonsQuery.Where(x => x.Grade == query.Grade.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Topic))
        {
            lessonsQuery = lessonsQuery.Where(x => x.Topic.Contains(query.Topic.Trim()));
        }

        var total = await lessonsQuery.CountAsync(cancellationToken);
        var items = await lessonsQuery
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new LessonSummaryResponse(
                x.Id,
                x.Title,
                x.Subject,
                x.Grade,
                x.Topic,
                x.Difficulty,
                x.CreatedAt,
                x.Questions.Count))
            .ToListAsync(cancellationToken);

        return new GetParentLessonsResult(items, total, page, pageSize);
    }
}