using MediatR;
using Microsoft.EntityFrameworkCore;

public sealed record DuplicateParentLessonCommand(Guid ParentId, Guid LessonId)
    : IRequest<DuplicateParentLessonResult>, IValidationFailureResponseFactory<DuplicateParentLessonResult>
{
    public DuplicateParentLessonResult CreateValidationFailureResponse(string error)
    {
        return DuplicateParentLessonResult.BadRequest(error);
    }
}

public sealed record DuplicateParentLessonResult(LessonDetailResponse? Lesson, string? Error, int StatusCode)
{
    public static DuplicateParentLessonResult BadRequest(string error) => new(null, error, StatusCodes.Status400BadRequest);

    public static DuplicateParentLessonResult NotFound(string error) => new(null, error, StatusCodes.Status404NotFound);

    public static DuplicateParentLessonResult Created(LessonDetailResponse lesson) => new(lesson, null, StatusCodes.Status201Created);
}

public sealed class DuplicateParentLessonCommandHandler : IRequestHandler<DuplicateParentLessonCommand, DuplicateParentLessonResult>
{
    private readonly AppDbContext _db;

    public DuplicateParentLessonCommandHandler(AppDbContext db)
    {
        _db = db;
    }

    public async Task<DuplicateParentLessonResult> Handle(DuplicateParentLessonCommand command, CancellationToken cancellationToken)
    {
        var scopedParentIds = await ApiEndpointHelpers.ResolveParentScopeIdsAsync(_db, command.ParentId);

        var sourceLesson = await _db.Lessons
            .AsNoTracking()
            .Include(x => x.Questions.OrderBy(q => q.Order))
            .ThenInclude(q => q.Answers.OrderBy(a => a.Order))
            .FirstOrDefaultAsync(x => x.Id == command.LessonId && scopedParentIds.Contains(x.CreatedBy), cancellationToken);

        if (sourceLesson is null)
        {
            return DuplicateParentLessonResult.NotFound("Lesson not found.");
        }

        var duplicatedLesson = new Lesson
        {
            Title = $"{sourceLesson.Title} (Copy)",
            Subject = sourceLesson.Subject,
            Grade = sourceLesson.Grade,
            Topic = sourceLesson.Topic,
            Difficulty = sourceLesson.Difficulty,
            Story = sourceLesson.Story,
            StoryImageUrl = sourceLesson.StoryImageUrl,
            CreatedBy = command.ParentId,
            CreatedAt = DateTime.UtcNow
        };

        foreach (var sourceQuestion in sourceLesson.Questions.OrderBy(q => q.Order))
        {
            var duplicatedQuestion = new Question
            {
                QuestionText = sourceQuestion.QuestionText,
                Explanation = sourceQuestion.Explanation,
                Order = sourceQuestion.Order
            };

            foreach (var sourceAnswer in sourceQuestion.Answers.OrderBy(a => a.Order))
            {
                duplicatedQuestion.Answers.Add(new AnswerOption
                {
                    AnswerText = sourceAnswer.AnswerText,
                    IsCorrect = sourceAnswer.IsCorrect,
                    Order = sourceAnswer.Order
                });
            }

            duplicatedLesson.Questions.Add(duplicatedQuestion);
        }

        _db.Lessons.Add(duplicatedLesson);
        await _db.SaveChangesAsync(cancellationToken);

        var response = new LessonDetailResponse(
            duplicatedLesson.Id,
            duplicatedLesson.Title,
            duplicatedLesson.Subject,
            duplicatedLesson.Grade,
            duplicatedLesson.Topic,
            duplicatedLesson.Difficulty,
            duplicatedLesson.CreatedAt,
            duplicatedLesson.Questions
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
            duplicatedLesson.Story,
            duplicatedLesson.StoryImageUrl);

        return DuplicateParentLessonResult.Created(response);
    }
}