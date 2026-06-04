using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public class AiLessonEditingIntegrationTests
{
    [Fact]
    public async Task Parent_CanEditAiLesson_AndRevisionIsCreated()
    {
        await using var factory = new KidsLearnApiFactory();
        using var client = factory.CreateClient();

        EnsureParentUser(factory.Services);
        var parentToken = CreateParentToken(factory.Services);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", parentToken);

        var generateResponse = await client.PostAsJsonAsync(
            "/api/v1/ai/lessons/generate",
            new GenerateAiLessonRequest("Math", 4, "Geometry", 2, "Easy", "en", new List<string> { "multiple-choice" }));

        Assert.Equal(HttpStatusCode.OK, generateResponse.StatusCode);
        var generated = await generateResponse.Content.ReadFromJsonAsync<GenerateAiLessonResponse>();
        Assert.NotNull(generated);

        var editRequest = new EditAiLessonRequest(
            "change-difficulty",
            new Dictionary<string, string> { ["difficulty"] = "Hard" },
            null);

        var editResponse = await client.PostAsJsonAsync($"/api/v1/ai/lessons/{generated!.CreatedLessonId}/edit", editRequest);
        Assert.Equal(HttpStatusCode.OK, editResponse.StatusCode);

        var editResult = await editResponse.Content.ReadFromJsonAsync<EditAiLessonResponse>();
        Assert.NotNull(editResult);
        Assert.Equal(1, editResult!.RevisionNumber);
        Assert.Equal("Hard", editResult.LessonDraft.Difficulty);
        Assert.Contains("Difficulty changed", editResult.DiffSummary);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var revisionsCount = db.LessonRevisions.Count(x => x.LessonId == generated.CreatedLessonId);
        Assert.Equal(1, revisionsCount);
    }

    [Fact]
    public async Task Parent_EditAiLesson_Returns400_ForUnsupportedCommand()
    {
        await using var factory = new KidsLearnApiFactory();
        using var client = factory.CreateClient();

        EnsureParentUser(factory.Services);
        var parentToken = CreateParentToken(factory.Services);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", parentToken);

        var generateResponse = await client.PostAsJsonAsync(
            "/api/v1/ai/lessons/generate",
            new GenerateAiLessonRequest("Math", 4, "Geometry", 1, "Easy", "en", new List<string> { "multiple-choice" }));

        Assert.Equal(HttpStatusCode.OK, generateResponse.StatusCode);
        var generated = await generateResponse.Content.ReadFromJsonAsync<GenerateAiLessonResponse>();
        Assert.NotNull(generated);

        var editResponse = await client.PostAsJsonAsync(
            $"/api/v1/ai/lessons/{generated!.CreatedLessonId}/edit",
            new EditAiLessonRequest("unsupported", null, null));

        Assert.Equal(HttpStatusCode.BadRequest, editResponse.StatusCode);
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
}
