using MediatR;
using Microsoft.EntityFrameworkCore;

public sealed record GetParentAiLessonRevisionsQuery(Guid ParentId, Guid LessonId)
    : IRequest<GetParentAiLessonRevisionsResult>;

public sealed record GetParentAiLessonRevisionsResult(List<AiLessonRevisionSummaryResponse>? Response, string? Error, int StatusCode)
{
    public static GetParentAiLessonRevisionsResult NotFound(string error)
        => new(null, error, StatusCodes.Status404NotFound);

    public static GetParentAiLessonRevisionsResult Success(List<AiLessonRevisionSummaryResponse> response)
        => new(response, null, StatusCodes.Status200OK);
}

public sealed class GetParentAiLessonRevisionsQueryHandler
    : IRequestHandler<GetParentAiLessonRevisionsQuery, GetParentAiLessonRevisionsResult>
{
    private readonly AppDbContext _db;

    public GetParentAiLessonRevisionsQueryHandler(AppDbContext db)
    {
        _db = db;
    }

    public async Task<GetParentAiLessonRevisionsResult> Handle(GetParentAiLessonRevisionsQuery query, CancellationToken cancellationToken)
    {
        var ownsLesson = await _db.Lessons
            .AsNoTracking()
            .AnyAsync(x => x.Id == query.LessonId && x.CreatedBy == query.ParentId, cancellationToken);

        if (!ownsLesson)
        {
            return GetParentAiLessonRevisionsResult.NotFound("Lesson not found.");
        }

        var revisions = await _db.LessonRevisions
            .AsNoTracking()
            .Where(x => x.LessonId == query.LessonId)
            .OrderByDescending(x => x.RevisionNumber)
            .Select(x => new AiLessonRevisionSummaryResponse(
                x.Id,
                x.RevisionNumber,
                x.DiffSummary,
                x.CreatedAt))
            .ToListAsync(cancellationToken);

        return GetParentAiLessonRevisionsResult.Success(revisions);
    }
}