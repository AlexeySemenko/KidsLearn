using System.Text;
using System.Text.Json;

public interface IAIProvider
{
    Task<GeneratedLessonDraft> GenerateLessonDraftAsync(GenerateAiLessonRequest request, CancellationToken cancellationToken = default);
}

public interface IAiLessonGenerationService
{
    Task<ServiceResult<GenerateAiLessonResponse>> GenerateAndPersistAsync(Guid parentId, GenerateAiLessonRequest request, CancellationToken cancellationToken = default);
}

public sealed record GeneratedLessonDraft(
    string Title,
    string Subject,
    int Grade,
    string Topic,
    string Difficulty,
    List<GeneratedQuestionDraft> Questions,
    AiProviderMetaResponse ProviderMeta);

public sealed record GeneratedQuestionDraft(
    string QuestionText,
    string Explanation,
    int Order,
    List<GeneratedAnswerDraft> Answers);

public sealed record GeneratedAnswerDraft(
    string AnswerText,
    bool IsCorrect,
    int Order);

public sealed class OpenAiProvider(IConfiguration configuration, HttpClient httpClient) : IAIProvider
{
    public async Task<GeneratedLessonDraft> GenerateLessonDraftAsync(GenerateAiLessonRequest request, CancellationToken cancellationToken = default)
    {
        var apiKey = configuration["OpenAI:ApiKey"];
        var model = configuration["OpenAI:Model"] ?? "gpt-4o-mini";

        // Deterministic fallback keeps local/dev and CI stable when API key is absent.
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return BuildFallbackDraft(request, model, "OpenAI key is not configured. Fallback generator is used.");
        }

        try
        {
            var prompt = BuildPrompt(request);
            var payload = JsonSerializer.Serialize(new
            {
                model,
                input = prompt
            });

            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses");
            requestMessage.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
            requestMessage.Content = new StringContent(payload, Encoding.UTF8, "application/json");

            using var response = await httpClient.SendAsync(requestMessage, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return BuildFallbackDraft(request, model, $"OpenAI returned {(int)response.StatusCode}. Fallback generator is used.");
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (TryParseDraftFromResponse(body, request, model, out var draft))
            {
                return draft;
            }

            return BuildFallbackDraft(request, model, "OpenAI response could not be parsed. Fallback generator is used.");
        }
        catch
        {
            return BuildFallbackDraft(request, model, "OpenAI request failed. Fallback generator is used.");
        }
    }

    private static string BuildPrompt(GenerateAiLessonRequest request)
    {
        var types = request.QuestionTypes is { Count: > 0 }
            ? string.Join(", ", request.QuestionTypes)
            : "multiple-choice";

        return $"Generate a strict JSON object with fields: title, subject, grade, topic, difficulty, questions. " +
               "Each question must include questionText, explanation, order, answers. " +
               "Each answers array must include at least 2 items with answerText, isCorrect, order and at least one correct answer. " +
               "No markdown, no comments. " +
               $"Subject: {request.Subject}; Grade: {request.Grade}; Topic: {request.Topic}; Difficulty: {request.Difficulty ?? "Medium"}; " +
               $"QuestionCount: {request.QuestionCount}; Language: {request.Language ?? "en"}; QuestionTypes: {types}.";
    }

    private static bool TryParseDraftFromResponse(string body, GenerateAiLessonRequest request, string model, out GeneratedLessonDraft draft)
    {
        draft = BuildFallbackDraft(request, model, "Fallback parser initialization.");

        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("output_text", out var outputTextElement))
        {
            return false;
        }

        var outputText = outputTextElement.GetString();
        if (string.IsNullOrWhiteSpace(outputText))
        {
            return false;
        }

        using var lessonDoc = JsonDocument.Parse(outputText);
        var root = lessonDoc.RootElement;

        if (!root.TryGetProperty("title", out var titleProp)
            || !root.TryGetProperty("questions", out var questionsProp)
            || questionsProp.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var questions = new List<GeneratedQuestionDraft>();
        var index = 1;
        foreach (var q in questionsProp.EnumerateArray())
        {
            if (!q.TryGetProperty("questionText", out var textProp)
                || !q.TryGetProperty("answers", out var answersProp)
                || answersProp.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            var answers = new List<GeneratedAnswerDraft>();
            var answerIndex = 1;
            foreach (var a in answersProp.EnumerateArray())
            {
                if (!a.TryGetProperty("answerText", out var answerTextProp)
                    || !a.TryGetProperty("isCorrect", out var isCorrectProp))
                {
                    return false;
                }

                answers.Add(new GeneratedAnswerDraft(
                    answerTextProp.GetString() ?? string.Empty,
                    isCorrectProp.GetBoolean(),
                    answerIndex++));
            }

            questions.Add(new GeneratedQuestionDraft(
                textProp.GetString() ?? string.Empty,
                q.TryGetProperty("explanation", out var explanationProp) ? explanationProp.GetString() ?? string.Empty : string.Empty,
                q.TryGetProperty("order", out var orderProp) && orderProp.TryGetInt32(out var parsedOrder) ? parsedOrder : index,
                answers));

            index++;
        }

        draft = new GeneratedLessonDraft(
            titleProp.GetString() ?? $"{request.Topic} Practice",
            request.Subject,
            request.Grade,
            request.Topic,
            request.Difficulty?.Trim() ?? "Medium",
            questions,
            new AiProviderMetaResponse("OpenAI", model, false, null));

        return true;
    }

    private static GeneratedLessonDraft BuildFallbackDraft(GenerateAiLessonRequest request, string model, string note)
    {
        var questionCount = Math.Clamp(request.QuestionCount, 1, 20);
        var difficulty = string.IsNullOrWhiteSpace(request.Difficulty) ? "Medium" : request.Difficulty.Trim();
        var language = string.IsNullOrWhiteSpace(request.Language) ? "en" : request.Language.Trim().ToLowerInvariant();

        var questions = Enumerable.Range(1, questionCount)
            .Select(i =>
            {
                var questionText = language.StartsWith("ru", StringComparison.Ordinal)
                    ? $"{request.Topic}: вопрос {i}"
                    : $"{request.Topic}: question {i}";

                var explanation = language.StartsWith("ru", StringComparison.Ordinal)
                    ? "Правильный ответ отмечен в вариантах."
                    : "Correct answer is marked in the options.";

                return new GeneratedQuestionDraft(
                    questionText,
                    explanation,
                    i,
                    new List<GeneratedAnswerDraft>
                    {
                        new($"Option A{i}", true, 1),
                        new($"Option B{i}", false, 2),
                        new($"Option C{i}", false, 3),
                        new($"Option D{i}", false, 4)
                    });
            })
            .ToList();

        return new GeneratedLessonDraft(
            $"{request.Topic} Practice ({difficulty})",
            request.Subject,
            request.Grade,
            request.Topic,
            difficulty,
            questions,
            new AiProviderMetaResponse("OpenAI", model, true, note));
    }
}

public sealed class AiLessonGenerationService(AppDbContext db, IAIProvider aiProvider) : IAiLessonGenerationService
{
    public async Task<ServiceResult<GenerateAiLessonResponse>> GenerateAndPersistAsync(Guid parentId, GenerateAiLessonRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Subject)
            || string.IsNullOrWhiteSpace(request.Topic)
            || request.Grade is < 1 or > 12
            || request.QuestionCount is < 1 or > 20)
        {
            return ServiceResult<GenerateAiLessonResponse>.Fail(400, "Subject, topic, grade (1-12) and questionCount (1-20) are required.");
        }

        var draft = await aiProvider.GenerateLessonDraftAsync(request, cancellationToken);

        if (draft.Questions.Count == 0)
        {
            return ServiceResult<GenerateAiLessonResponse>.Fail(422, "AI returned an empty lesson draft.");
        }

        foreach (var question in draft.Questions)
        {
            if (string.IsNullOrWhiteSpace(question.QuestionText) || question.Answers.Count < 2 || !question.Answers.Any(x => x.IsCorrect))
            {
                return ServiceResult<GenerateAiLessonResponse>.Fail(422, "AI returned invalid draft schema for questions/answers.");
            }
        }

        var lesson = new Lesson
        {
            Title = string.IsNullOrWhiteSpace(draft.Title) ? $"{request.Topic} Practice" : draft.Title.Trim(),
            Subject = request.Subject.Trim(),
            Grade = request.Grade,
            Topic = request.Topic.Trim(),
            Difficulty = string.IsNullOrWhiteSpace(request.Difficulty) ? "Medium" : request.Difficulty.Trim(),
            CreatedBy = parentId,
            CreatedAt = DateTime.UtcNow
        };

        foreach (var questionDraft in draft.Questions.OrderBy(x => x.Order))
        {
            var question = new Question
            {
                QuestionText = questionDraft.QuestionText.Trim(),
                Explanation = questionDraft.Explanation?.Trim() ?? string.Empty,
                Order = questionDraft.Order
            };

            foreach (var answerDraft in questionDraft.Answers.OrderBy(x => x.Order))
            {
                question.Answers.Add(new AnswerOption
                {
                    AnswerText = answerDraft.AnswerText.Trim(),
                    IsCorrect = answerDraft.IsCorrect,
                    Order = answerDraft.Order
                });
            }

            lesson.Questions.Add(question);
        }

        db.Lessons.Add(lesson);
        await db.SaveChangesAsync(cancellationToken);

        var response = new GenerateAiLessonResponse(
            lesson.Id,
            new LessonDetailResponse(
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
                    .ToList()),
            draft.ProviderMeta);

        return ServiceResult<GenerateAiLessonResponse>.Success(response);
    }
}
