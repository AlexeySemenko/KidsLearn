using System.Text;
using System.Text.Json;

public interface IAIProvider
{
    Task<GeneratedLessonDraft> GenerateLessonDraftAsync(GenerateAiLessonRequest request, CancellationToken cancellationToken = default);
}

public sealed class AiSchemaValidationException(string message) : Exception(message);

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

            for (var attempt = 1; attempt <= 2; attempt++)
            {
                using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses");
                requestMessage.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
                requestMessage.Content = new StringContent(payload, Encoding.UTF8, "application/json");

                using var response = await httpClient.SendAsync(requestMessage, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    var isTransient = (int)response.StatusCode is 429 or 500 or 502 or 503 or 504;
                    if (attempt < 2 && isTransient)
                    {
                        continue;
                    }

                    return BuildFallbackDraft(request, model, $"OpenAI returned {(int)response.StatusCode}. Fallback generator is used.");
                }

                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                if (TryParseDraftFromResponse(body, request, model, out var draft, out var parseError))
                {
                    return draft;
                }

                throw new AiSchemaValidationException(parseError ?? "Unknown AI schema validation error.");
            }

            return BuildFallbackDraft(request, model, "OpenAI response retry policy exhausted. Fallback generator is used.");
        }
        catch (AiSchemaValidationException)
        {
            throw;
        }
        catch
        {
            return BuildFallbackDraft(request, model, "OpenAI request failed. Fallback generator is used.");
        }
    }

    private static string BuildPrompt(GenerateAiLessonRequest request)
    {
        var types = request.QuestionTypes is { Count: > 0 }
            ? string.Join(", ", request.QuestionTypes).ToLowerInvariant()
            : "multiple-choice";

        string answerInstruction;
        if (types.Contains("true-false"))
        {
            answerInstruction = "Every question must have exactly 2 answer options (True and False), with exactly 1 marked isCorrect=true.";
        }
        else if (types.Contains("mixed"))
        {
            answerInstruction = "Mix question formats: roughly half the questions should be multiple-choice with exactly 4 answer options (exactly 1 marked isCorrect=true), and the other half true/false with exactly 2 answer options (True and False, exactly 1 marked isCorrect=true).";
        }
        else
        {
            answerInstruction = "Every question must have exactly 4 answer options, with exactly 1 marked isCorrect=true and the remaining 3 marked isCorrect=false.";
        }

        return $"Generate a strict JSON object with fields: title, subject, grade, topic, difficulty, questions. " +
               "Each question must include questionText, explanation, order, answers. " +
               "Each answer must include answerText, isCorrect (boolean), order. " +
               $"{answerInstruction} " +
               "Output valid JSON only — no markdown, no code fences, no comments. " +
               $"Subject: {request.Subject}; Grade: {request.Grade}; Topic: {request.Topic}; Difficulty: {request.Difficulty ?? "Medium"}; " +
               $"QuestionCount: {request.QuestionCount}; Language: {request.Language ?? "en"}.";
    }

    private static bool TryParseDraftFromResponse(string body, GenerateAiLessonRequest request, string model, out GeneratedLessonDraft draft, out string? error)
    {
        draft = BuildFallbackDraft(request, model, "Fallback parser initialization.");
        error = null;

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(body);
        }
        catch (JsonException ex)
        {
            error = $"OpenAI transport payload is not valid JSON: {ex.Message}";
            return false;
        }

        using (doc)
        {
        if (!TryExtractGeneratedText(doc.RootElement, out var outputText, out var extractionError))
        {
            error = extractionError;
            return false;
        }

        if (string.IsNullOrWhiteSpace(outputText))
        {
            error = "Field 'output_text' is empty in OpenAI response.";
            return false;
        }

        JsonDocument lessonDoc;
        try
        {
            lessonDoc = JsonDocument.Parse(outputText);
        }
        catch (JsonException ex)
        {
            error = $"AI lesson payload is not valid JSON: {ex.Message}";
            return false;
        }

        using (lessonDoc)
        {
        var root = lessonDoc.RootElement;

        if (!root.TryGetProperty("title", out var titleProp)
            || !root.TryGetProperty("questions", out var questionsProp)
            || questionsProp.ValueKind != JsonValueKind.Array)
        {
            error = "AI lesson payload must contain 'title' and array 'questions'.";
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
                error = $"Question #{index} must contain 'questionText' and array 'answers'.";
                return false;
            }

            var answers = new List<GeneratedAnswerDraft>();
            var answerIndex = 1;
            foreach (var a in answersProp.EnumerateArray())
            {
                if (!a.TryGetProperty("answerText", out var answerTextProp)
                    || !a.TryGetProperty("isCorrect", out var isCorrectProp))
                {
                    error = $"Question #{index}, answer #{answerIndex} must contain 'answerText' and 'isCorrect'.";
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

        if (questions.Count == 0)
        {
            error = "AI lesson payload must contain at least one question.";
            return false;
        }

        draft = new GeneratedLessonDraft(
            titleProp.GetString() ?? $"{request.Topic} Practice",
            request.Subject,
            request.Grade,
            request.Topic,
            request.Difficulty?.Trim() ?? "Medium",
            questions,
            new AiProviderMetaResponse("OpenAI", model, false, null));

        }
        }

        return true;
    }

    private static bool TryExtractGeneratedText(JsonElement root, out string? text, out string? error)
    {
        text = null;
        error = null;

        if (root.TryGetProperty("output_text", out var outputTextElement)
            && outputTextElement.ValueKind == JsonValueKind.String)
        {
            text = outputTextElement.GetString();
            return true;
        }

        if (!root.TryGetProperty("output", out var outputElement)
            || outputElement.ValueKind != JsonValueKind.Array)
        {
            error = "Missing both 'output_text' and array 'output' in OpenAI response.";
            return false;
        }

        var builder = new StringBuilder();
        foreach (var outputItem in outputElement.EnumerateArray())
        {
            if (!outputItem.TryGetProperty("content", out var contentElement)
                || contentElement.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var contentItem in contentElement.EnumerateArray())
            {
                if (contentItem.TryGetProperty("text", out var textElement)
                    && textElement.ValueKind == JsonValueKind.String)
                {
                    builder.AppendLine(textElement.GetString());
                }
            }
        }

        text = builder.ToString().Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            error = "OpenAI response did not include textual output in 'output' content blocks.";
            return false;
        }

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

        GeneratedLessonDraft draft;
        try
        {
            draft = await aiProvider.GenerateLessonDraftAsync(request, cancellationToken);
        }
        catch (AiSchemaValidationException ex)
        {
            return ServiceResult<GenerateAiLessonResponse>.Fail(422, $"AI schema validation failed: {ex.Message}");
        }

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
