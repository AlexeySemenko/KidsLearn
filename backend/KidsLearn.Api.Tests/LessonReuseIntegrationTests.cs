using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public class LessonReuseIntegrationTests
{
    [Fact]
    public async Task Parent_CanDuplicateOwnLesson_WithQuestionsAndAnswers()
    {
        await using var factory = new KidsLearnApiFactory();
        using var client = factory.CreateClient();

        EnsureParentUser(factory.Services);
        var parentToken = CreateParentToken(factory.Services);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", parentToken);

        var createLessonResponse = await client.PostAsJsonAsync("/api/v1/lessons", BuildLessonRequest("Original Reuse Lesson"));
        Assert.Equal(HttpStatusCode.Created, createLessonResponse.StatusCode);

        var createdLesson = await createLessonResponse.Content.ReadFromJsonAsync<LessonSummaryResponse>();
        Assert.NotNull(createdLesson);

        var originalDetailResponse = await client.GetAsync($"/api/v1/lessons/{createdLesson!.Id}");
        Assert.Equal(HttpStatusCode.OK, originalDetailResponse.StatusCode);
        var original = await originalDetailResponse.Content.ReadFromJsonAsync<LessonDetailResponse>();
        Assert.NotNull(original);

        var duplicateResponse = await client.PostAsync($"/api/v1/lessons/{createdLesson.Id}/duplicate", null);
        Assert.Equal(HttpStatusCode.Created, duplicateResponse.StatusCode);

        var duplicate = await duplicateResponse.Content.ReadFromJsonAsync<LessonDetailResponse>();
        Assert.NotNull(duplicate);

        Assert.NotEqual(original!.Id, duplicate!.Id);
        Assert.Equal("Original Reuse Lesson (Copy)", duplicate.Title);
        Assert.Equal(original.Subject, duplicate.Subject);
        Assert.Equal(original.Grade, duplicate.Grade);
        Assert.Equal(original.Topic, duplicate.Topic);
        Assert.Equal(original.Difficulty, duplicate.Difficulty);
        Assert.Equal(original.Questions.Count, duplicate.Questions.Count);

        var originalQuestion = original.Questions[0];
        var duplicatedQuestion = duplicate.Questions[0];

        Assert.NotEqual(originalQuestion.Id, duplicatedQuestion.Id);
        Assert.Equal(originalQuestion.QuestionText, duplicatedQuestion.QuestionText);
        Assert.Equal(originalQuestion.Explanation, duplicatedQuestion.Explanation);
        Assert.Equal(originalQuestion.Answers.Count, duplicatedQuestion.Answers.Count);
        Assert.NotEqual(originalQuestion.Answers[0].Id, duplicatedQuestion.Answers[0].Id);
        Assert.Equal(originalQuestion.Answers[0].AnswerText, duplicatedQuestion.Answers[0].AnswerText);
        Assert.Equal(originalQuestion.Answers[0].IsCorrect, duplicatedQuestion.Answers[0].IsCorrect);
    }

    private static CreateLessonRequest BuildLessonRequest(string title)
    {
        return new CreateLessonRequest(
            title,
            "Math",
            4,
            "Multiplication",
            "Medium",
            new List<CreateQuestionRequest>
            {
                new(
                    "3*4=?",
                    "12",
                    1,
                    new List<CreateAnswerOptionRequest>
                    {
                        new("12", true, 1),
                        new("14", false, 2)
                    }),
                new(
                    "5*5=?",
                    "25",
                    2,
                    new List<CreateAnswerOptionRequest>
                    {
                        new("20", false, 1),
                        new("25", true, 2)
                    })
            });
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
