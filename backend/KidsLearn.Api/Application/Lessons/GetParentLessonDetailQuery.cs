using MediatR;
using Microsoft.EntityFrameworkCore;

public sealed record GetParentLessonDetailQuery(Guid ParentId, Guid LessonId) : IRequest<GetParentLessonDetailResult>;

public sealed record GetParentLessonDetailResult(LessonDetailResponse? Lesson, int StatusCode)
{
    public static GetParentLessonDetailResult NotFound()
        => new(null, StatusCodes.Status404NotFound);

    public static GetParentLessonDetailResult Success(LessonDetailResponse lesson)
        => new(lesson, StatusCodes.Status200OK);
}

public sealed class GetParentLessonDetailQueryHandler : IRequestHandler<GetParentLessonDetailQuery, GetParentLessonDetailResult>
{
    private readonly AppDbContext _db;

    public GetParentLessonDetailQueryHandler(AppDbContext db)
    {
        _db = db;
    }

    public async Task<GetParentLessonDetailResult> Handle(GetParentLessonDetailQuery query, CancellationToken cancellationToken)
    {
        var scopedParentIds = await ApiEndpointHelpers.ResolveParentScopeIdsAsync(_db, query.ParentId);

        var lesson = await _db.Lessons
            .AsNoTracking()
            .Include(x => x.Questions.OrderBy(q => q.Order))
            .ThenInclude(q => q.Answers.OrderBy(a => a.Order))
            .FirstOrDefaultAsync(x => x.Id == query.LessonId && scopedParentIds.Contains(x.CreatedBy), cancellationToken);

        if (lesson is null)
        {
            return GetParentLessonDetailResult.NotFound();
        }

        var response = new LessonDetailResponse(
            lesson.Id,
            lesson.Title,
            lesson.Subject,
            lesson.Grade,
            lesson.Topic,
            lesson.Difficulty,
            lesson.CreatedAt,
            lesson.Questions
                .OrderBy(q => q.Order)
                .Select(q => new QuestionResponse(
                    q.Id,
                    q.QuestionText,
                    q.Explanation,
                    q.Order,
                    q.Answers
                        .OrderBy(a => a.Order)
                        .Select(a => new AnswerOptionResponse(a.Id, a.AnswerText, a.IsCorrect, a.Order))
                        .ToList()))
                .ToList(),
            lesson.Story,
            lesson.StoryImageUrl != null);

        return GetParentLessonDetailResult.Success(response);
    }
}