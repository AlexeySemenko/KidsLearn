using MediatR;

public sealed record CreateParentLessonCommand(Guid ParentId, CreateLessonRequest Request)
    : IRequest<CreateParentLessonResult>, IValidationFailureResponseFactory<CreateParentLessonResult>
{
    public CreateParentLessonResult CreateValidationFailureResponse(string error)
    {
        return CreateParentLessonResult.BadRequest(error);
    }
}

public sealed record CreateParentLessonResult(LessonSummaryResponse? Lesson, string? Error, int StatusCode)
{
    public static CreateParentLessonResult BadRequest(string error) => new(null, error, StatusCodes.Status400BadRequest);

    public static CreateParentLessonResult Created(LessonSummaryResponse lesson) => new(lesson, null, StatusCodes.Status201Created);
}

public sealed class CreateParentLessonCommandHandler : IRequestHandler<CreateParentLessonCommand, CreateParentLessonResult>
{
    private readonly AppDbContext _db;

    public CreateParentLessonCommandHandler(AppDbContext db)
    {
        _db = db;
    }

    public async Task<CreateParentLessonResult> Handle(CreateParentLessonCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;

        var lesson = new Lesson
        {
            Title = request.Title!.Trim(),
            Subject = request.Subject!.Trim(),
            Grade = request.Grade,
            Topic = request.Topic!.Trim(),
            Difficulty = string.IsNullOrWhiteSpace(request.Difficulty) ? "Medium" : request.Difficulty.Trim(),
            Story = string.IsNullOrWhiteSpace(request.Story) ? null : request.Story.Trim(),
            CreatedBy = command.ParentId,
            CreatedAt = DateTime.UtcNow
        };

        for (var i = 0; i < request.Questions.Count; i++)
        {
            var sourceQuestion = request.Questions[i];

            var question = new Question
            {
                QuestionText = sourceQuestion.QuestionText.Trim(),
                Explanation = sourceQuestion.Explanation?.Trim() ?? string.Empty,
                Order = sourceQuestion.Order ?? (i + 1)
            };

            for (var answerIndex = 0; answerIndex < sourceQuestion.Answers.Count; answerIndex++)
            {
                var sourceAnswer = sourceQuestion.Answers[answerIndex];
                question.Answers.Add(new AnswerOption
                {
                    AnswerText = sourceAnswer.AnswerText.Trim(),
                    IsCorrect = sourceAnswer.IsCorrect,
                    Order = sourceAnswer.Order ?? (answerIndex + 1)
                });
            }

            lesson.Questions.Add(question);
        }

        _db.Lessons.Add(lesson);
        await _db.SaveChangesAsync(cancellationToken);

        var response = new LessonSummaryResponse(
            lesson.Id,
            lesson.Title,
            lesson.Subject,
            lesson.Grade,
            lesson.Topic,
            lesson.Difficulty,
            lesson.CreatedAt,
            lesson.Questions.Count,
            null);

        return CreateParentLessonResult.Created(response);
    }
}