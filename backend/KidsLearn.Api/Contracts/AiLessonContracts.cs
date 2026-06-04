public sealed record GenerateAiLessonRequest(
    string Subject,
    int Grade,
    string Topic,
    int QuestionCount,
    string? Difficulty,
    string? Language,
    List<string>? QuestionTypes);

public sealed record AiProviderMetaResponse(
    string Provider,
    string Model,
    bool FallbackUsed,
    string? Note);

public sealed record GenerateAiLessonResponse(
    Guid CreatedLessonId,
    LessonDetailResponse LessonDraft,
    AiProviderMetaResponse ProviderMeta);
