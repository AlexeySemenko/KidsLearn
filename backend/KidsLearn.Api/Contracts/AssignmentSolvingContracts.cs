public sealed record SubmitAnswerRequest(Guid QuestionId, Guid? SelectedAnswerOptionId, string? TextAnswer);

public sealed record SubmitAssignmentAnswersRequest(List<SubmitAnswerRequest> Answers);

public sealed record InstantCheckItemResponse(Guid QuestionId, bool Correct, string Explanation);

public sealed record SubmitAssignmentAnswersResponse(List<InstantCheckItemResponse> InstantCheck, decimal PartialScore);

public sealed record CompleteAssignmentResponse(Guid ResultId, decimal Score, DateTime CompletedAt, int CorrectAnswers, int TotalQuestions);

public sealed record ResultBreakdownAnswerResponse(Guid AnswerId, string AnswerText, bool IsCorrect);

public sealed record ResultBreakdownItemResponse(
    Guid QuestionId,
    string QuestionText,
    bool Correct,
    Guid? SelectedAnswerOptionId,
    List<ResultBreakdownAnswerResponse> Answers);

public sealed record ResultDetailResponse(
    Guid ResultId,
    Guid AssignmentId,
    string LessonTitle,
    decimal Score,
    DateTime CompletedAt,
    int CorrectAnswers,
    int TotalQuestions,
    List<ResultBreakdownItemResponse> Breakdown);

public sealed record ResultListItemResponse(
    Guid ResultId,
    Guid AssignmentId,
    string LessonTitle,
    string Subject,
    string Topic,
    int Grade,
    decimal Score,
    DateTime CompletedAt,
    int CorrectAnswers,
    int TotalQuestions);

public sealed record AssignmentQuestionAnswerResponse(Guid AnswerId, string AnswerText, int Order);

public sealed record AssignmentQuestionResponse(Guid QuestionId, string QuestionText, string Explanation, int Order, List<AssignmentQuestionAnswerResponse> Answers);

public sealed record AssignmentForSolvingResponse(
    Guid AssignmentId,
    Guid ChildId,
    Guid LessonId,
    DateTime AssignedAt,
    DateTime? DueDate,
    string Status,
    string LessonTitle,
    List<AssignmentQuestionResponse> Questions);
