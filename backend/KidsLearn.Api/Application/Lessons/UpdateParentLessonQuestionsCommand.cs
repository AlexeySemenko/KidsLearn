using MediatR;
using Microsoft.EntityFrameworkCore;

public sealed record UpdateParentLessonQuestionsCommand(Guid ParentId, Guid LessonId, UpdateLessonQuestionsRequest Request)
    : IRequest<UpdateParentLessonQuestionsResult>;

public sealed record UpdateParentLessonQuestionsResult(LessonDetailResponse? Lesson, string? Error, int StatusCode)
{
    public static UpdateParentLessonQuestionsResult BadRequest(string error) => new(null, error, StatusCodes.Status400BadRequest);
    public static UpdateParentLessonQuestionsResult NotFound(string error) => new(null, error, StatusCodes.Status404NotFound);
    public static UpdateParentLessonQuestionsResult Ok(LessonDetailResponse lesson) => new(lesson, null, StatusCodes.Status200OK);
}

public sealed class UpdateParentLessonQuestionsCommandHandler : IRequestHandler<UpdateParentLessonQuestionsCommand, UpdateParentLessonQuestionsResult>
{
    private readonly AppDbContext _db;

    public UpdateParentLessonQuestionsCommandHandler(AppDbContext db)
    {
        _db = db;
    }

    public async Task<UpdateParentLessonQuestionsResult> Handle(UpdateParentLessonQuestionsCommand command, CancellationToken cancellationToken)
    {
        var scopedParentIds = await ApiEndpointHelpers.ResolveParentScopeIdsAsync(_db, command.ParentId);

        var lesson = await _db.Lessons.FirstOrDefaultAsync(
            x => x.Id == command.LessonId && scopedParentIds.Contains(x.CreatedBy),
            cancellationToken);

        if (lesson is null)
        {
            return UpdateParentLessonQuestionsResult.NotFound("Lesson not found.");
        }

        var requestItems = command.Request.Questions;

        // Validate
        for (var i = 0; i < requestItems.Count; i++)
        {
            var item = requestItems[i];
            if (string.IsNullOrWhiteSpace(item.QuestionText))
                return UpdateParentLessonQuestionsResult.BadRequest($"Question {i + 1}: question text is required.");
            if (item.Answers.Count < 2)
                return UpdateParentLessonQuestionsResult.BadRequest($"Question {i + 1}: at least 2 answers are required.");
            if (!item.Answers.Any(a => a.IsCorrect))
                return UpdateParentLessonQuestionsResult.BadRequest($"Question {i + 1}: at least one correct answer is required.");
            if (item.Answers.Any(a => string.IsNullOrWhiteSpace(a.AnswerText)))
                return UpdateParentLessonQuestionsResult.BadRequest($"Question {i + 1}: answer text cannot be empty.");
        }

        var existingQuestions = await _db.Questions
            .Include(q => q.Answers)
            .Where(q => q.LessonId == command.LessonId)
            .ToListAsync(cancellationToken);

        var existingQuestionIds = existingQuestions.Select(q => q.Id).ToList();

        var answeredQuestionIds = existingQuestionIds.Count > 0
            ? await _db.AssignmentAnswers
                .Where(a => existingQuestionIds.Contains(a.QuestionId))
                .Select(a => a.QuestionId)
                .Distinct()
                .ToListAsync(cancellationToken)
            : [];

        var requestedIds = requestItems
            .Where(r => r.Id.HasValue)
            .Select(r => r.Id!.Value)
            .ToHashSet();

        // Remove questions not in request that haven't been answered
        foreach (var eq in existingQuestions)
        {
            if (!requestedIds.Contains(eq.Id) && !answeredQuestionIds.Contains(eq.Id))
            {
                _db.Questions.Remove(eq);
            }
        }

        // Upsert questions in request order
        for (var order = 0; order < requestItems.Count; order++)
        {
            var item = requestItems[order];

            if (item.Id.HasValue)
            {
                var existing = existingQuestions.FirstOrDefault(q => q.Id == item.Id.Value);
                if (existing is not null)
                {
                    existing.QuestionText = item.QuestionText.Trim();
                    existing.Explanation = item.Explanation?.Trim() ?? string.Empty;
                    existing.Order = order;

                    // Replace all answers (SetNull cascade handles AssignmentAnswer.SelectedAnswerOptionId)
                    _db.AnswerOptions.RemoveRange(existing.Answers);
                    var newAnswers = item.Answers.Select((a, ai) => new AnswerOption
                    {
                        QuestionId = existing.Id,
                        AnswerText = a.AnswerText.Trim(),
                        IsCorrect = a.IsCorrect,
                        Order = ai,
                    }).ToList();
                    _db.AnswerOptions.AddRange(newAnswers);
                    continue;
                }
            }

            // New question (no ID or ID not found)
            var newQuestion = new Question
            {
                LessonId = command.LessonId,
                QuestionText = item.QuestionText.Trim(),
                Explanation = item.Explanation?.Trim() ?? string.Empty,
                Order = order,
                Answers = item.Answers.Select((a, ai) => new AnswerOption
                {
                    AnswerText = a.AnswerText.Trim(),
                    IsCorrect = a.IsCorrect,
                    Order = ai,
                }).ToList(),
            };
            _db.Questions.Add(newQuestion);
        }

        await _db.SaveChangesAsync(cancellationToken);

        // Reload updated state
        var updatedQuestions = await _db.Questions
            .Include(q => q.Answers)
            .Where(q => q.LessonId == command.LessonId)
            .OrderBy(q => q.Order)
            .ToListAsync(cancellationToken);

        var response = new LessonDetailResponse(
            lesson.Id,
            lesson.Title,
            lesson.Subject,
            lesson.Grade,
            lesson.Topic,
            lesson.Difficulty,
            lesson.CreatedAt,
            updatedQuestions.Select(q => new QuestionResponse(
                q.Id,
                q.QuestionText,
                q.Explanation,
                q.Order,
                q.Answers.OrderBy(a => a.Order)
                    .Select(a => new AnswerOptionResponse(a.Id, a.AnswerText, a.IsCorrect, a.Order))
                    .ToList()
            )).ToList(),
            lesson.Story,
            lesson.StoryImageUrl);

        return UpdateParentLessonQuestionsResult.Ok(response);
    }
}
