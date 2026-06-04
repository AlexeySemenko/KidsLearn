using MediatR;

public sealed record CreateParentLessonCommand(Guid ParentId, CreateLessonRequest Request) : IRequest<CreateParentLessonResult>;

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

        var titleValidation = ApiEndpointHelpers.ValidateRequiredNonEmpty(request.Title, "Title, subject, topic and grade (1-12) are required.");
        if (titleValidation is not null)
        {
            return CreateParentLessonResult.BadRequest("Title, subject, topic and grade (1-12) are required.");
        }

        var subjectValidation = ApiEndpointHelpers.ValidateRequiredNonEmpty(request.Subject, "Title, subject, topic and grade (1-12) are required.");
        if (subjectValidation is not null)
        {
            return CreateParentLessonResult.BadRequest("Title, subject, topic and grade (1-12) are required.");
        }

        var topicValidation = ApiEndpointHelpers.ValidateRequiredNonEmpty(request.Topic, "Title, subject, topic and grade (1-12) are required.");
        if (topicValidation is not null)
        {
            return CreateParentLessonResult.BadRequest("Title, subject, topic and grade (1-12) are required.");
        }

        if (!ApiEndpointHelpers.IsGradeInRange(request.Grade))
        {
            return CreateParentLessonResult.BadRequest("Title, subject, topic and grade (1-12) are required.");
        }

        if (request.Questions is null || request.Questions.Count == 0)
        {
            return CreateParentLessonResult.BadRequest("At least one question is required.");
        }

        var lesson = new Lesson
        {
            Title = request.Title!.Trim(),
            Subject = request.Subject!.Trim(),
            Grade = request.Grade,
            Topic = request.Topic!.Trim(),
            Difficulty = string.IsNullOrWhiteSpace(request.Difficulty) ? "Medium" : request.Difficulty.Trim(),
            CreatedBy = command.ParentId,
            CreatedAt = DateTime.UtcNow
        };

        for (var i = 0; i < request.Questions.Count; i++)
        {
            var sourceQuestion = request.Questions[i];
            if (string.IsNullOrWhiteSpace(sourceQuestion.QuestionText)
                || sourceQuestion.Answers is null
                || sourceQuestion.Answers.Count < 2)
            {
                return CreateParentLessonResult.BadRequest("Each question must have text and at least two answers.");
            }

            if (!sourceQuestion.Answers.Any(x => x.IsCorrect))
            {
                return CreateParentLessonResult.BadRequest("Each question must include at least one correct answer.");
            }

            var question = new Question
            {
                QuestionText = sourceQuestion.QuestionText.Trim(),
                Explanation = sourceQuestion.Explanation?.Trim() ?? string.Empty,
                Order = sourceQuestion.Order ?? (i + 1)
            };

            for (var answerIndex = 0; answerIndex < sourceQuestion.Answers.Count; answerIndex++)
            {
                var sourceAnswer = sourceQuestion.Answers[answerIndex];
                if (string.IsNullOrWhiteSpace(sourceAnswer.AnswerText))
                {
                    return CreateParentLessonResult.BadRequest("Answer text is required.");
                }

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
            lesson.Questions.Count);

        return CreateParentLessonResult.Created(response);
    }
}