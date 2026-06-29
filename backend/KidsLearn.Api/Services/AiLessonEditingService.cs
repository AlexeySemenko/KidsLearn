using System.Text.Json;
using Microsoft.EntityFrameworkCore;

public interface IAiLessonEditingService
{
    Task<ServiceResult<EditAiLessonResponse>> EditAsync(Guid parentId, Guid lessonId, EditAiLessonRequest request, CancellationToken cancellationToken = default);
}

public sealed class AiLessonEditingService(AppDbContext db) : IAiLessonEditingService
{
    public async Task<ServiceResult<EditAiLessonResponse>> EditAsync(Guid parentId, Guid lessonId, EditAiLessonRequest request, CancellationToken cancellationToken = default)
    {
        var scopedParentIds = await ApiEndpointHelpers.ResolveParentScopeIdsAsync(db, parentId);

        if (string.IsNullOrWhiteSpace(request.Command))
        {
            return ServiceResult<EditAiLessonResponse>.Fail(400, "Command is required.");
        }

        var lesson = await db.Lessons
            .Include(x => x.Questions.OrderBy(q => q.Order))
            .ThenInclude(q => q.Answers.OrderBy(a => a.Order))
            .FirstOrDefaultAsync(x => x.Id == lessonId && scopedParentIds.Contains(x.CreatedBy), cancellationToken);

        if (lesson is null)
        {
            return ServiceResult<EditAiLessonResponse>.Fail(404, "Lesson not found.");
        }

        var snapshotBefore = ToLessonDetail(lesson);
        var revisionNumber = await db.LessonRevisions
            .Where(x => x.LessonId == lesson.Id)
            .Select(x => (int?)x.RevisionNumber)
            .MaxAsync(cancellationToken) ?? 0;

        string diffSummary;
        var command = request.Command.Trim().ToLowerInvariant();
        switch (command)
        {
            case "change-difficulty":
                if (request.Params is null || !request.Params.TryGetValue("difficulty", out var difficulty) || string.IsNullOrWhiteSpace(difficulty))
                {
                    return ServiceResult<EditAiLessonResponse>.Fail(400, "Params.difficulty is required for change-difficulty.");
                }

                lesson.Difficulty = difficulty.Trim();
                diffSummary = $"Difficulty changed to '{lesson.Difficulty}'.";
                break;

            case "add-question":
                if (request.Params is null || !request.Params.TryGetValue("questionText", out var questionText) || string.IsNullOrWhiteSpace(questionText))
                {
                    return ServiceResult<EditAiLessonResponse>.Fail(400, "Params.questionText is required for add-question.");
                }

                var explanation = request.Params.TryGetValue("explanation", out var exp)
                    ? exp?.Trim() ?? string.Empty
                    : string.Empty;

                var answers = request.Answers ??
                    new List<EditAiAnswerInput>
                    {
                        new("Option A", true),
                        new("Option B", false)
                    };

                if (answers.Count < 2 || !answers.Any(x => x.IsCorrect) || answers.Any(x => string.IsNullOrWhiteSpace(x.AnswerText)))
                {
                    return ServiceResult<EditAiLessonResponse>.Fail(400, "Answers must contain at least two non-empty options with at least one correct answer.");
                }

                var nextOrder = lesson.Questions.Count == 0 ? 1 : lesson.Questions.Max(q => q.Order) + 1;
                var question = new Question
                {
                    QuestionText = questionText.Trim(),
                    Explanation = explanation,
                    Order = nextOrder
                };

                for (var i = 0; i < answers.Count; i++)
                {
                    question.Answers.Add(new AnswerOption
                    {
                        AnswerText = answers[i].AnswerText.Trim(),
                        IsCorrect = answers[i].IsCorrect,
                        Order = i + 1
                    });
                }

                lesson.Questions.Add(question);
                diffSummary = $"Question added with order {nextOrder}.";
                break;

            case "remove-question":
                if (request.Params is null || !request.Params.TryGetValue("questionId", out var questionIdText) || !Guid.TryParse(questionIdText, out var questionId))
                {
                    return ServiceResult<EditAiLessonResponse>.Fail(400, "Params.questionId must be a valid GUID for remove-question.");
                }

                var toRemove = lesson.Questions.FirstOrDefault(q => q.Id == questionId);
                if (toRemove is null)
                {
                    return ServiceResult<EditAiLessonResponse>.Fail(404, "Question not found in lesson.");
                }

                db.Questions.Remove(toRemove);
                diffSummary = $"Question {questionId} removed.";
                break;

            case "regenerate-explanations":
                foreach (var q in lesson.Questions)
                {
                    q.Explanation = $"Review: {q.QuestionText}";
                }

                diffSummary = "Explanations regenerated for all questions.";
                break;

            default:
                return ServiceResult<EditAiLessonResponse>.Fail(400, "Unsupported command. Use change-difficulty, add-question, remove-question, or regenerate-explanations.");
        }

        var revision = new LessonRevision
        {
            LessonId = lesson.Id,
            RevisionNumber = revisionNumber + 1,
            SnapshotJson = JsonSerializer.Serialize(snapshotBefore),
            DiffSummary = diffSummary,
            CreatedAt = DateTime.UtcNow
        };

        db.LessonRevisions.Add(revision);
        await db.SaveChangesAsync(cancellationToken);

        var current = await db.Lessons
            .AsNoTracking()
            .Include(x => x.Questions.OrderBy(q => q.Order))
            .ThenInclude(q => q.Answers.OrderBy(a => a.Order))
            .FirstAsync(x => x.Id == lesson.Id, cancellationToken);

        return ServiceResult<EditAiLessonResponse>.Success(new EditAiLessonResponse(
            revision.Id,
            revision.RevisionNumber,
            diffSummary,
            ToLessonDetail(current)));
    }

    private static LessonDetailResponse ToLessonDetail(Lesson lesson)
    {
        return new LessonDetailResponse(
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
            lesson.StoryImageUrl);
    }
}
