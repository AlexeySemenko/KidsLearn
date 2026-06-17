public sealed record CreateAnswerOptionRequest(string AnswerText, bool IsCorrect, int? Order);

public sealed record CreateQuestionRequest(string QuestionText, string? Explanation, int? Order, List<CreateAnswerOptionRequest> Answers);

public sealed record CreateLessonRequest(
    string Title,
    string Subject,
    int Grade,
    string Topic,
    string? Difficulty,
    List<CreateQuestionRequest> Questions);

public sealed record UpdateLessonRequest(
    string? Title,
    string? Subject,
    int? Grade,
    string? Topic,
    string? Difficulty);

public sealed record AnswerOptionResponse(Guid Id, string AnswerText, bool IsCorrect, int Order);

public sealed record QuestionResponse(Guid Id, string QuestionText, string Explanation, int Order, List<AnswerOptionResponse> Answers);

public sealed record LessonSummaryResponse(
    Guid Id,
    string Title,
    string Subject,
    int Grade,
    string Topic,
    string Difficulty,
    DateTime CreatedAt,
    int QuestionCount,
    string? CreatedByName);

public sealed record LessonDetailResponse(
    Guid Id,
    string Title,
    string Subject,
    int Grade,
    string Topic,
    string Difficulty,
    DateTime CreatedAt,
    List<QuestionResponse> Questions);
