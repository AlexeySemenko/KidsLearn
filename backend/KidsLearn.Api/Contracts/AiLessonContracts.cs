public sealed record GenerateAiLessonRequest(
    string Subject,
    int Grade,
    string Topic,
    int QuestionCount,
    string? Difficulty,
    string? Language,
    List<string>? QuestionTypes,
    bool? IncludeStory = null,
    string? PreGeneratedStory = null,
    string? PreGeneratedStoryImageUrl = null);

public sealed record GenerateStoryRequest(
    string Subject,
    int Grade,
    string Topic,
    string? Difficulty = null,
    string? Language = null);

public sealed record GenerateStoryResponse(string Story, string? StoryImageUrl);

public sealed record AiProviderMetaResponse(
    string Provider,
    string Model,
    bool FallbackUsed,
    string? Note);

public sealed record GenerateAiLessonResponse(
    Guid CreatedLessonId,
    LessonDetailResponse LessonDraft,
    AiProviderMetaResponse ProviderMeta);

public sealed record EditAiLessonRequest(
    string Command,
    Dictionary<string, string>? Params,
    List<EditAiAnswerInput>? Answers);

public sealed record EditAiAnswerInput(string AnswerText, bool IsCorrect);

public sealed record EditAiLessonResponse(
    Guid RevisionId,
    int RevisionNumber,
    string DiffSummary,
    LessonDetailResponse LessonDraft);

public sealed record AiLessonRevisionSummaryResponse(
    Guid RevisionId,
    int RevisionNumber,
    string DiffSummary,
    DateTime CreatedAt);
