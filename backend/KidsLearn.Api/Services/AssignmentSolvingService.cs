using Microsoft.EntityFrameworkCore;

public enum AssignmentAccessScope
{
    Parent,
    Child
}

public sealed class ServiceResult<T>
{
    public bool IsSuccess { get; init; }
    public int StatusCode { get; init; }
    public string? Error { get; init; }
    public T? Value { get; init; }

    public static ServiceResult<T> Success(T value) => new() { IsSuccess = true, StatusCode = 200, Value = value };
    public static ServiceResult<T> Fail(int statusCode, string error) => new() { IsSuccess = false, StatusCode = statusCode, Error = error };
}

public interface IAssignmentSolvingService
{
    Task<ServiceResult<AssignmentForSolvingResponse>> GetForSolvingAsync(AssignmentAccessScope scope, Guid scopeId, Guid assignmentId);
    Task<ServiceResult<SubmitAssignmentAnswersResponse>> SubmitAnswersAsync(AssignmentAccessScope scope, Guid scopeId, Guid assignmentId, SubmitAssignmentAnswersRequest request);
    Task<ServiceResult<CompleteAssignmentResponse>> CompleteAsync(AssignmentAccessScope scope, Guid scopeId, Guid assignmentId);
    Task<ServiceResult<ResultDetailResponse>> GetResultAsync(AssignmentAccessScope scope, Guid scopeId, Guid resultId);
    Task<List<ResultListItemResponse>> GetResultsAsync(Guid childId);
    Task<List<ResultListItemResponse>> GetChildResultsForParentAsync(Guid parentId, Guid childId);
}

public class AssignmentSolvingService(AppDbContext db) : IAssignmentSolvingService
{
    public async Task<ServiceResult<AssignmentForSolvingResponse>> GetForSolvingAsync(AssignmentAccessScope scope, Guid scopeId, Guid assignmentId)
    {
        HashSet<Guid>? parentScopeIds = null;
        if (scope == AssignmentAccessScope.Parent)
        {
            parentScopeIds = await ApiEndpointHelpers.ResolveParentScopeIdsAsync(db, scopeId);
        }

        var assignment = await db.Assignments
            .AsNoTracking()
            .Include(x => x.Lesson)
            .ThenInclude(x => x.Questions.OrderBy(q => q.Order))
            .ThenInclude(q => q.Answers.OrderBy(a => a.Order))
            .FirstOrDefaultAsync(AccessPredicate(scope, scopeId, assignmentId, parentScopeIds));

        if (assignment is null)
        {
            return ServiceResult<AssignmentForSolvingResponse>.Fail(404, "Assignment not found.");
        }

        var response = new AssignmentForSolvingResponse(
            assignment.Id,
            assignment.ChildId,
            assignment.LessonId,
            assignment.AssignedAt,
            assignment.DueDate,
            assignment.Status,
            assignment.Lesson.Title,
            assignment.Lesson.Questions
                .OrderBy(q => q.Order)
                .Select(q => new AssignmentQuestionResponse(
                    q.Id,
                    q.QuestionText,
                    q.Explanation,
                    q.Order,
                    q.Answers
                        .OrderBy(a => a.Order)
                        .Select(a => new AssignmentQuestionAnswerResponse(a.Id, a.AnswerText, a.Order))
                        .ToList()))
                .ToList(),
            assignment.Lesson.Story);

        return ServiceResult<AssignmentForSolvingResponse>.Success(response);
    }

    public async Task<ServiceResult<SubmitAssignmentAnswersResponse>> SubmitAnswersAsync(AssignmentAccessScope scope, Guid scopeId, Guid assignmentId, SubmitAssignmentAnswersRequest request)
    {
        HashSet<Guid>? parentScopeIds = null;
        if (scope == AssignmentAccessScope.Parent)
        {
            parentScopeIds = await ApiEndpointHelpers.ResolveParentScopeIdsAsync(db, scopeId);
        }

        if (request.Answers is null || request.Answers.Count == 0)
        {
            return ServiceResult<SubmitAssignmentAnswersResponse>.Fail(400, "At least one answer is required.");
        }

        var assignment = await db.Assignments
            .Include(x => x.Lesson)
            .ThenInclude(x => x.Questions)
            .ThenInclude(q => q.Answers)
            .FirstOrDefaultAsync(AccessPredicate(scope, scopeId, assignmentId, parentScopeIds));

        if (assignment is null)
        {
            return ServiceResult<SubmitAssignmentAnswersResponse>.Fail(404, "Assignment not found.");
        }

        var questionsById = assignment.Lesson.Questions.ToDictionary(q => q.Id);
        var instantCheck = new List<InstantCheckItemResponse>();

        foreach (var answer in request.Answers)
        {
            if (!questionsById.TryGetValue(answer.QuestionId, out var question))
            {
                return ServiceResult<SubmitAssignmentAnswersResponse>.Fail(400, "Question does not belong to assignment lesson.");
            }

            var normalizedTextAnswer = string.IsNullOrWhiteSpace(answer.TextAnswer)
                ? null
                : answer.TextAnswer.Trim();

            var isCorrect = false;
            if (answer.SelectedAnswerOptionId.HasValue)
            {
                var selected = question.Answers.FirstOrDefault(x => x.Id == answer.SelectedAnswerOptionId.Value);
                if (selected is null)
                {
                    return ServiceResult<SubmitAssignmentAnswersResponse>.Fail(400, "Selected answer option does not belong to question.");
                }

                isCorrect = selected.IsCorrect;
            }
            else if (!string.IsNullOrWhiteSpace(normalizedTextAnswer))
            {
                isCorrect = question.Answers.Any(x => x.IsCorrect && string.Equals(x.AnswerText.Trim(), normalizedTextAnswer, StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                return ServiceResult<SubmitAssignmentAnswersResponse>.Fail(400, "Either SelectedAnswerOptionId or TextAnswer is required for each answer.");
            }

            var existing = await db.AssignmentAnswers
                .FirstOrDefaultAsync(x => x.AssignmentId == assignment.Id && x.QuestionId == answer.QuestionId);

            if (existing is null)
            {
                db.AssignmentAnswers.Add(new AssignmentAnswer
                {
                    AssignmentId = assignment.Id,
                    QuestionId = answer.QuestionId,
                    SelectedAnswerOptionId = answer.SelectedAnswerOptionId,
                    TextAnswer = normalizedTextAnswer,
                    IsCorrect = isCorrect,
                    SubmittedAt = DateTime.UtcNow
                });
            }
            else
            {
                existing.SelectedAnswerOptionId = answer.SelectedAnswerOptionId;
                existing.TextAnswer = normalizedTextAnswer;
                existing.IsCorrect = isCorrect;
                existing.SubmittedAt = DateTime.UtcNow;
            }

            instantCheck.Add(new InstantCheckItemResponse(question.Id, isCorrect, question.Explanation));
        }

        if (assignment.Status == "Assigned")
        {
            assignment.Status = "InProgress";
        }

        await db.SaveChangesAsync();

        var totalQuestions = assignment.Lesson.Questions.Count;
        var correctCount = await db.AssignmentAnswers.CountAsync(x => x.AssignmentId == assignment.Id && x.IsCorrect);
        var partialScore = totalQuestions == 0 ? 0 : Math.Round(100m * correctCount / totalQuestions, 2);

        return ServiceResult<SubmitAssignmentAnswersResponse>.Success(new SubmitAssignmentAnswersResponse(instantCheck, partialScore));
    }

    public async Task<ServiceResult<CompleteAssignmentResponse>> CompleteAsync(AssignmentAccessScope scope, Guid scopeId, Guid assignmentId)
    {
        HashSet<Guid>? parentScopeIds = null;
        if (scope == AssignmentAccessScope.Parent)
        {
            parentScopeIds = await ApiEndpointHelpers.ResolveParentScopeIdsAsync(db, scopeId);
        }

        var assignment = await db.Assignments
            .Include(x => x.Lesson)
            .ThenInclude(x => x.Questions)
            .FirstOrDefaultAsync(AccessPredicate(scope, scopeId, assignmentId, parentScopeIds));

        if (assignment is null)
        {
            return ServiceResult<CompleteAssignmentResponse>.Fail(404, "Assignment not found.");
        }

        var totalQuestions = assignment.Lesson.Questions.Count;
        var correctCount = await db.AssignmentAnswers.CountAsync(x => x.AssignmentId == assignment.Id && x.IsCorrect);
        var score = totalQuestions == 0 ? 0 : Math.Round(100m * correctCount / totalQuestions, 2);
        var completedAt = DateTime.UtcNow;

        var result = await db.Results.FirstOrDefaultAsync(x => x.AssignmentId == assignment.Id);
        if (result is null)
        {
            result = new AssignmentResult
            {
                AssignmentId = assignment.Id,
                Score = score,
                CorrectAnswers = correctCount,
                TotalQuestions = totalQuestions,
                CompletedAt = completedAt
            };
            db.Results.Add(result);
        }
        else
        {
            result.Score = score;
            result.CorrectAnswers = correctCount;
            result.TotalQuestions = totalQuestions;
            result.CompletedAt = completedAt;
        }

        assignment.Status = "Completed";
        await db.SaveChangesAsync();

        return ServiceResult<CompleteAssignmentResponse>.Success(new CompleteAssignmentResponse(
            result.Id,
            result.Score,
            result.CompletedAt,
            result.CorrectAnswers,
            result.TotalQuestions));
    }

    public async Task<ServiceResult<ResultDetailResponse>> GetResultAsync(AssignmentAccessScope scope, Guid scopeId, Guid resultId)
    {
        HashSet<Guid>? parentScopeIds = null;
        if (scope == AssignmentAccessScope.Parent)
        {
            parentScopeIds = await ApiEndpointHelpers.ResolveParentScopeIdsAsync(db, scopeId);
        }

        var result = scope == AssignmentAccessScope.Parent
            ? await db.Results
                .AsNoTracking()
                .Include(x => x.Assignment)
                    .ThenInclude(x => x.Lesson)
                        .ThenInclude(x => x.Questions.OrderBy(q => q.Order))
                            .ThenInclude(q => q.Answers.OrderBy(a => a.Order))
                .FirstOrDefaultAsync(x => x.Id == resultId && parentScopeIds!.Contains(x.Assignment.Child.ParentId))
            : await db.Results
                .AsNoTracking()
                .Include(x => x.Assignment)
                    .ThenInclude(x => x.Lesson)
                        .ThenInclude(x => x.Questions.OrderBy(q => q.Order))
                            .ThenInclude(q => q.Answers.OrderBy(a => a.Order))
                .FirstOrDefaultAsync(x => x.Id == resultId && x.Assignment.ChildId == scopeId);

        if (result is null)
        {
            return ServiceResult<ResultDetailResponse>.Fail(404, "Result not found.");
        }

        var submittedAnswers = await db.AssignmentAnswers
            .AsNoTracking()
            .Where(x => x.AssignmentId == result.AssignmentId)
            .ToListAsync();

        var latestByQuestion = submittedAnswers
            .GroupBy(x => x.QuestionId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.SubmittedAt).First());

        var breakdown = result.Assignment.Lesson.Questions
            .OrderBy(q => q.Order)
            .Select(q =>
            {
                var submitted = latestByQuestion.GetValueOrDefault(q.Id);
                var answers = q.Answers.OrderBy(a => a.Order)
                    .Select(a => new ResultBreakdownAnswerResponse(a.Id, a.AnswerText, a.IsCorrect))
                    .ToList();
                return new ResultBreakdownItemResponse(
                    q.Id,
                    q.QuestionText,
                    submitted?.IsCorrect ?? false,
                    submitted?.SelectedAnswerOptionId,
                    answers);
            })
            .ToList();

        return ServiceResult<ResultDetailResponse>.Success(new ResultDetailResponse(
            result.Id,
            result.AssignmentId,
            result.Assignment.Lesson.Title,
            result.Score,
            result.CompletedAt,
            result.CorrectAnswers,
            result.TotalQuestions,
            breakdown));
    }

    public async Task<List<ResultListItemResponse>> GetResultsAsync(Guid childId)
    {
        return await db.Results
            .AsNoTracking()
            .Include(x => x.Assignment)
                .ThenInclude(x => x.Lesson)
            .Where(x => x.Assignment.ChildId == childId)
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
            .ToListAsync();
    }

    public async Task<List<ResultListItemResponse>> GetChildResultsForParentAsync(Guid parentId, Guid childId)
    {
        var owned = await ApiEndpointHelpers.EnsureParentOwnsChildAsync(db, parentId, childId);
        if (!owned)
        {
            return [];
        }

        return await db.Results
            .AsNoTracking()
            .Include(x => x.Assignment)
                .ThenInclude(x => x.Lesson)
            .Where(x => x.Assignment.ChildId == childId)
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
            .ToListAsync();
    }

    private static System.Linq.Expressions.Expression<Func<Assignment, bool>> AccessPredicate(AssignmentAccessScope scope, Guid scopeId, Guid assignmentId, HashSet<Guid>? parentScopeIds)
        => scope == AssignmentAccessScope.Parent
            ? x => x.Id == assignmentId && parentScopeIds!.Contains(x.Child.ParentId)
            : x => x.Id == assignmentId && x.ChildId == scopeId;
}
