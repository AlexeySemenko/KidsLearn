using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

public class AiLessonGenerationIntegrationTests
{
    [Fact]
    public async Task Parent_CanGenerateAiLesson_AndLessonIsPersisted()
    {
        await using var factory = new KidsLearnApiFactory();
        using var client = factory.CreateClient();

        EnsureParentUser(factory.Services);
        var parentToken = CreateParentToken(factory.Services);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", parentToken);

        var request = new GenerateAiLessonRequest(
            "Math",
            3,
            "Fractions",
            3,
            "Easy",
            "en",
            new List<string> { "multiple-choice" });

        var generateResponse = await client.PostAsJsonAsync("/api/v1/ai/lessons/generate", request);
        Assert.Equal(HttpStatusCode.OK, generateResponse.StatusCode);

        var generated = await generateResponse.Content.ReadFromJsonAsync<GenerateAiLessonResponse>();
        Assert.NotNull(generated);
        Assert.NotEqual(Guid.Empty, generated!.CreatedLessonId);
        Assert.Equal(3, generated.LessonDraft.Questions.Count);
        Assert.NotNull(generated.ProviderMeta.Provider);

        var persistedLessonResponse = await client.GetAsync($"/api/v1/lessons/{generated.CreatedLessonId}");
        Assert.Equal(HttpStatusCode.OK, persistedLessonResponse.StatusCode);
        var persistedLesson = await persistedLessonResponse.Content.ReadFromJsonAsync<LessonDetailResponse>();
        Assert.NotNull(persistedLesson);
        Assert.Equal(3, persistedLesson!.Questions.Count);
    }

    [Fact]
    public async Task Parent_GenerateAiLesson_Returns422_WhenProviderSchemaIsInvalid()
    {
        await using var factory = new KidsLearnApiFactory(services =>
        {
            services.RemoveAll<IAIProvider>();
            services.AddScoped<IAIProvider, InvalidSchemaAiProvider>();
        });
        using var client = factory.CreateClient();

        EnsureParentUser(factory.Services);
        var parentToken = CreateParentToken(factory.Services);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", parentToken);

        var request = new GenerateAiLessonRequest(
            "Math",
            3,
            "Fractions",
            3,
            "Easy",
            "en",
            new List<string> { "multiple-choice" });

        var response = await client.PostAsJsonAsync("/api/v1/ai/lessons/generate", request);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.NotNull(payload);
        Assert.True(payload!.TryGetValue("error", out var error));
        Assert.Contains("AI schema validation failed", error);
        Assert.Contains("Question #1", error);
    }

    private static void EnsureParentUser(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (db.Users.Any(x => x.Email == "parent.test@example.com"))
        {
            return;
        }

        var hasher = new PasswordHasherService();
        db.Users.Add(new AppUser
        {
            Email = "parent.test@example.com",
            PasswordHash = hasher.HashPassword("Parent123!"),
            Role = UserRole.Parent,
            CreatedAt = DateTime.UtcNow
        });

        db.SaveChanges();
    }

    private static string CreateParentToken(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tokenService = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();

        var user = db.Users.First(x => x.Email == "parent.test@example.com");
        return tokenService.CreateAccessToken(user);
    }

    private sealed class InvalidSchemaAiProvider : IAIProvider
    {
        public Task<GeneratedLessonDraft> GenerateLessonDraftAsync(GenerateAiLessonRequest request, string? validatorFeedback = null, CancellationToken cancellationToken = default)
        {
            throw new AiSchemaValidationException("Question #1 must contain 'questionText' and array 'answers'.");
        }

        public Task<DraftValidationResult?> ValidateDraftAsync(GenerateAiLessonRequest request, GeneratedLessonDraft draft, CancellationToken cancellationToken = default)
            => Task.FromResult<DraftValidationResult?>(null);
    }
}
