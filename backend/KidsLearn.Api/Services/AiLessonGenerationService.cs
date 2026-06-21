using System.Text;
using System.Text.Json;

public interface IAIProvider
{
    Task<GeneratedLessonDraft> GenerateLessonDraftAsync(GenerateAiLessonRequest request, string? validatorFeedback = null, CancellationToken cancellationToken = default);
    Task<DraftValidationResult?> ValidateDraftAsync(GenerateAiLessonRequest request, GeneratedLessonDraft draft, CancellationToken cancellationToken = default);
}

/// <summary>
/// Validation agent result.
/// Score is 0-100 (percentage of acceptance).
/// QuestionsToRegenerate contains 1-based question indices where story-answer mismatch was detected.
/// </summary>
public sealed record DraftValidationResult(
    bool Accepted,
    int Score,
    string[] Issues,
    string Feedback,
    int[] QuestionsToRegenerate);

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
    AiProviderMetaResponse ProviderMeta,
    string? Story = null);

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
    public async Task<GeneratedLessonDraft> GenerateLessonDraftAsync(GenerateAiLessonRequest request, string? validatorFeedback = null, CancellationToken cancellationToken = default)
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
            var prompt = BuildPrompt(request, validatorFeedback);
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

    public async Task<DraftValidationResult?> ValidateDraftAsync(GenerateAiLessonRequest request, GeneratedLessonDraft draft, CancellationToken cancellationToken = default)
    {
        var apiKey = configuration["OpenAI:ApiKey"];
        var model = configuration["OpenAI:Model"] ?? "gpt-4o-mini";

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return null; // no key — skip validation
        }

        try
        {
            var prompt = BuildValidationPrompt(request, draft);
            var payload = JsonSerializer.Serialize(new { model, input = prompt });

            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses");
            requestMessage.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
            requestMessage.Content = new StringContent(payload, Encoding.UTF8, "application/json");

            using var response = await httpClient.SendAsync(requestMessage, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null; // validation unavailable — skip
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return TryParseValidationResult(body);
        }
        catch
        {
            return null; // validation agent failure — skip, don't block generation
        }
    }

    private static string BuildValidationPrompt(GenerateAiLessonRequest request, GeneratedLessonDraft draft)
    {
        // Number questions explicitly so the validator can reference them by 1-based index.
        var numberedQuestions = draft.Questions.Select((q, i) => new
        {
            question_number = i + 1,
            q.QuestionText,
            answers = q.Answers.Select(a => new { a.AnswerText, a.IsCorrect })
        });
        var questionsJson = JsonSerializer.Serialize(numberedQuestions);

        var storySection = draft.Story is not null
            ? $"\nStory: {draft.Story}"
            : "\nStory: (none)";

        var storyCheckInstruction = draft.Story is not null
            ? "8. STORY ANSWER CHECK (CRITICAL): For each question, count how many of its answer options " +
              "are NOT mentioned or derivable from the story. If MORE THAN 2 answers of a single question " +
              "cannot be found in or inferred from the story, that question must be listed in 'questions_to_regenerate' " +
              "(use the 1-based question_number). This check applies to every question individually.\n"
            : "";

        var jsonShape = draft.Story is not null
            ? """{"accepted": true/false, "score": <0-100>, "issues": ["issue1"], "feedback": "<instructions or empty>", "questions_to_regenerate": [1, 3]}"""
            : """{"accepted": true/false, "score": <0-100>, "issues": ["issue1"], "feedback": "<instructions or empty>", "questions_to_regenerate": []}""";

        return
            "You are a strict lesson quality validator. Evaluate the following AI-generated lesson content against the requested parameters.\n\n" +
            "REQUESTED PARAMETERS:\n" +
            $"- Subject: {request.Subject}\n" +
            $"- Grade: {request.Grade}\n" +
            $"- Topic: {request.Topic}\n" +
            $"- Difficulty: {request.Difficulty ?? "Medium"}\n" +
            $"- Language: {request.Language ?? "en"}\n" +
            $"- QuestionCount: {request.QuestionCount}\n" +
            $"- IncludeStory: {(request.IncludeStory == true ? "yes" : "no")}\n\n" +
            "GENERATED CONTENT:\n" +
            $"- Title: {draft.Title}\n" +
            $"- Questions ({draft.Questions.Count}): {questionsJson}{storySection}\n\n" +
            "CHECK THE FOLLOWING (flag as CRITICAL if violated):\n" +
            "1. Questions match the requested subject and topic.\n" +
            $"2. Language of all text matches requested language ('{request.Language ?? "en"}').\n" +
            $"3. Difficulty level is appropriate for grade {request.Grade}.\n" +
            $"4. Exactly {request.QuestionCount} question(s) are present.\n" +
            "5. Each question has at least one correct answer marked.\n" +
            "6. If story was requested, story is present and all questions relate to it.\n" +
            "7. No question is trivially easy or nonsensical.\n" +
            storyCheckInstruction + "\n" +
            "Respond with a strict JSON object only — no markdown, no code fences, no comments:\n" +
            jsonShape + "\n\n" +
            "Rules for the response:\n" +
            "- Set accepted=false only for CRITICAL flaws: language mismatch, completely wrong topic, or story-answer mismatch on more than half the questions.\n" +
            "- When accepted=false, 'feedback' MUST be a non-empty string describing exactly what needs to be fixed.\n" +
            "- When accepted=true, 'feedback' should be empty string.\n" +
            "- Populate 'questions_to_regenerate' ONLY when accepted=false and specific questions fail the story-answer check (rule 8).\n" +
            "- When accepted=true, 'questions_to_regenerate' must be an empty array [].";
    }

    private static DraftValidationResult? TryParseValidationResult(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            // Support both direct JSON and the OpenAI responses API wrapper
            string? jsonText = null;
            if (root.TryGetProperty("output_text", out var outputTextEl) && outputTextEl.ValueKind == JsonValueKind.String)
            {
                jsonText = outputTextEl.GetString();
            }
            else if (root.TryGetProperty("output", out var outputEl) && outputEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in outputEl.EnumerateArray())
                {
                    if (item.TryGetProperty("content", out var contentEl) && contentEl.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var c in contentEl.EnumerateArray())
                        {
                            if (c.TryGetProperty("text", out var textEl) && textEl.ValueKind == JsonValueKind.String)
                            {
                                jsonText = textEl.GetString();
                                break;
                            }
                        }
                    }
                    if (jsonText is not null) break;
                }
            }

            if (string.IsNullOrWhiteSpace(jsonText)) return null;

            // Strip markdown code fences if present
            var trimmed = jsonText.Trim();
            if (trimmed.StartsWith("```")) trimmed = System.Text.RegularExpressions.Regex.Replace(trimmed, @"^```[a-z]*\n?", "").TrimEnd('`').Trim();

            using var valDoc = JsonDocument.Parse(trimmed);
            var val = valDoc.RootElement;

            var accepted = val.TryGetProperty("accepted", out var accProp) && accProp.GetBoolean();
            var score = val.TryGetProperty("score", out var scoreProp) && scoreProp.TryGetInt32(out var s) ? s : 50;
            var issues = val.TryGetProperty("issues", out var issuesProp) && issuesProp.ValueKind == JsonValueKind.Array
                ? issuesProp.EnumerateArray().Select(i => i.GetString() ?? "").Where(i => i.Length > 0).ToArray()
                : [];
            var feedback = val.TryGetProperty("feedback", out var fbProp) ? fbProp.GetString() ?? "" : "";
            var questionsToRegen = val.TryGetProperty("questions_to_regenerate", out var qtrProp) && qtrProp.ValueKind == JsonValueKind.Array
                ? qtrProp.EnumerateArray()
                    .Where(e => e.TryGetInt32(out _))
                    .Select(e => { e.TryGetInt32(out var n); return n; })
                    .Where(n => n > 0)
                    .Distinct()
                    .OrderBy(n => n)
                    .ToArray()
                : [];

            return new DraftValidationResult(accepted, Math.Clamp(score, 0, 100), issues, feedback, questionsToRegen);
        }
        catch
        {
            return null;
        }
    }

    private static string BuildPrompt(GenerateAiLessonRequest request, string? validatorFeedback = null)
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

        var storyInstruction = request.IncludeStory == true
            ? $"Also include a 'story' field: a short engaging narrative paragraph (number of sentences {request.Grade + 3} to  {request.Grade + 5}) that sets the scene for the lesson topic. Take into consideration the grade = {request.Grade} of the child, make a story difficulty according to grade. Add a twist of humor or surprise to make it engaging. All questions must be strictly related to facts in this story. An answer to each question MUST be present in or inferable from the story. If a question's answer cannot be found in or inferred from the story, it must be listed in 'questions_to_regenerate' (1-based index)."
            : "";

        var feedbackSection = !string.IsNullOrWhiteSpace(validatorFeedback)
            ? $" IMPORTANT — a previous attempt was rejected by the quality validator. You MUST address these issues: {validatorFeedback}"
            : "";

        return $"Generate a strict JSON object with fields: title, subject, grade, topic, difficulty, questions{(request.IncludeStory == true ? ", story" : "")}. " +
               "Each question must include questionText, explanation, order, answers. " +
               "Each answer must include answerText, isCorrect (boolean), order. " +
               $"{answerInstruction} " +
               $"{storyInstruction}" +
               "Output valid JSON only — no markdown, no code fences, no comments. " +
               $"Subject: {request.Subject}; Grade: {request.Grade}; Topic: {request.Topic}; Difficulty: {request.Difficulty ?? "Medium"}; " +
               $"QuestionCount: {request.QuestionCount}; Language: {request.Language ?? "en"}." +
               feedbackSection;
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

        var story = root.TryGetProperty("story", out var storyProp) && storyProp.ValueKind == JsonValueKind.String
            ? storyProp.GetString()?.Trim()
            : null;

        draft = new GeneratedLessonDraft(
            titleProp.GetString() ?? $"{request.Topic} Practice",
            request.Subject,
            request.Grade,
            request.Topic,
            request.Difficulty?.Trim() ?? "Medium",
            questions,
            new AiProviderMetaResponse("OpenAI", model, false, null),
            story);

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

public sealed class AiLessonGenerationService(AppDbContext db, IAIProvider aiProvider, ILogger<AiLessonGenerationService> logger) : IAiLessonGenerationService
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
            // --- Initial generation ---
            draft = await aiProvider.GenerateLessonDraftAsync(request, null, cancellationToken);

            // --- Validation + regeneration loop (max 3 cycles) ---
            if (!draft.ProviderMeta.FallbackUsed)
            {
                for (var cycle = 1; cycle <= 3; cycle++)
                {
                    var validation = await aiProvider.ValidateDraftAsync(request, draft, cancellationToken);

                    if (validation is null)
                    {
                        logger.LogInformation(
                            "AI validation agent unavailable or skipped (cycle {Cycle}) for Subject: {Subject}, Topic: {Topic}.",
                            cycle, request.Subject, request.Topic);
                        break;
                    }

                    logger.LogInformation(
                        "AI validation cycle {Cycle} — Subject: {Subject}, Topic: {Topic}, Grade: {Grade} | " +
                        "Accepted: {Accepted}, Score: {Score}%, QuestionsToRegenerate: [{QTR}], Issues: [{Issues}], Feedback: {Feedback}",
                        cycle, request.Subject, request.Topic, request.Grade,
                        validation.Accepted, validation.Score,
                        string.Join(", ", validation.QuestionsToRegenerate),
                        string.Join("; ", validation.Issues),
                        validation.Feedback);

                    if (validation.Accepted)
                    {
                        // Validator approved — stop regardless of QuestionsToRegenerate.
                        logger.LogInformation("AI validation cycle {Cycle}: accepted. Using draft.", cycle);
                        break;
                    }

                    // Not accepted — build targeted or general feedback.
                    string? feedback;

                    if (validation.QuestionsToRegenerate.Length > 0)
                    {
                        var badList = string.Join(", ", validation.QuestionsToRegenerate);
                        var storyRef = draft.Story is not null
                            ? $" The story is: \"{draft.Story}\""
                            : "";
                        var extra = !string.IsNullOrWhiteSpace(validation.Feedback) ? $" Additionally: {validation.Feedback}" : "";

                        feedback =
                            $"Questions {badList} have more than 2 answer options that are not present in or inferable from the story." +
                            $"{storyRef} Regenerate ONLY questions {badList} so that every answer option can be found in or clearly inferred from the story.{extra}";

                        logger.LogInformation(
                            "AI validation cycle {Cycle}: regenerating questions [{BadQ}] for story-answer mismatch.",
                            cycle, badList);
                    }
                    else if (!string.IsNullOrWhiteSpace(validation.Feedback))
                    {
                        feedback = validation.Feedback;
                        logger.LogInformation(
                            "AI validation cycle {Cycle}: full regeneration (score {Score}%).",
                            cycle, validation.Score);
                    }
                    else
                    {
                        // Not accepted but no actionable feedback — cannot improve, stop.
                        logger.LogInformation("AI validation cycle {Cycle}: not accepted but no actionable feedback. Using draft.", cycle);
                        break;
                    }

                    draft = await aiProvider.GenerateLessonDraftAsync(request, feedback, cancellationToken);

                    if (cycle == 3)
                    {
                        logger.LogInformation("AI validation: max cycles (3) reached. Using last generated draft.");
                    }
                }
            }
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
            Story = string.IsNullOrWhiteSpace(draft.Story) ? null : draft.Story,
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
                    .ToList(),
                lesson.Story),
            draft.ProviderMeta);

        return ServiceResult<GenerateAiLessonResponse>.Success(response);
    }
}
