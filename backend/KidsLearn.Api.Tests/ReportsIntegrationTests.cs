using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public class ReportsIntegrationTests
{
    [Fact]
    public async Task Parent_CanGetChildReportSummary()
    {
        await using var factory = new KidsLearnApiFactory();
        using var client = factory.CreateClient();

        EnsureParentUser(factory.Services);
        var parentToken = CreateParentToken(factory.Services);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", parentToken);

        var childResponse = await client.PostAsJsonAsync("/api/v1/children", new CreateChildRequest("ReportChild", 5, "3333"));
        Assert.Equal(HttpStatusCode.Created, childResponse.StatusCode);
        var child = await childResponse.Content.ReadFromJsonAsync<CreatedChildResponse>();
        Assert.NotNull(child);

        var lessonResponse = await client.PostAsJsonAsync("/api/v1/lessons", BuildLessonRequest("Report Lesson"));
        Assert.Equal(HttpStatusCode.Created, lessonResponse.StatusCode);
        var lesson = await lessonResponse.Content.ReadFromJsonAsync<LessonSummaryResponse>();
        Assert.NotNull(lesson);

        var assignmentResponse = await client.PostAsJsonAsync("/api/v1/assignments", new CreateAssignmentRequest(child!.Child.Id, lesson!.Id, DateTime.UtcNow.AddDays(2)));
        Assert.Equal(HttpStatusCode.Created, assignmentResponse.StatusCode);
        var assignment = await assignmentResponse.Content.ReadFromJsonAsync<AssignmentResponse>();
        Assert.NotNull(assignment);

        var solvingResponse = await client.GetAsync($"/api/v1/assignments/{assignment!.Id}/for-solving");
        Assert.Equal(HttpStatusCode.OK, solvingResponse.StatusCode);
        var solving = await solvingResponse.Content.ReadFromJsonAsync<AssignmentForSolvingResponse>();
        Assert.NotNull(solving);

        var question = solving!.Questions[0];
        var correctAnswer = question.Answers.First(x => x.Order == 1);

        var submitResponse = await client.PostAsJsonAsync(
            $"/api/v1/assignments/{assignment.Id}/answers",
            new SubmitAssignmentAnswersRequest(new List<SubmitAnswerRequest> { new(question.QuestionId, correctAnswer.AnswerId, null) }));
        Assert.Equal(HttpStatusCode.OK, submitResponse.StatusCode);

        var completeResponse = await client.PostAsync($"/api/v1/assignments/{assignment.Id}/complete", null);
        Assert.Equal(HttpStatusCode.OK, completeResponse.StatusCode);

        var reportResponse = await client.GetAsync($"/api/v1/reports/children/{child.Child.Id}");
        Assert.Equal(HttpStatusCode.OK, reportResponse.StatusCode);

        var report = await reportResponse.Content.ReadFromJsonAsync<ChildReportSummaryResponse>();
        Assert.NotNull(report);
        Assert.Equal(child.Child.Id, report!.ChildId);
        Assert.Equal(1, report.SolvedCount);
        Assert.Equal(100m, report.CompletionRate);
        Assert.Equal(100m, report.AverageScore);
        Assert.Equal(0, report.StreakDays); // completed today — today is excluded from streak by design
    }

    [Fact]
    public async Task Parent_CanExportChildReportCsv()
    {
        await using var factory = new KidsLearnApiFactory();
        using var client = factory.CreateClient();

        EnsureParentUser(factory.Services);
        var parentToken = CreateParentToken(factory.Services);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", parentToken);

        var childResponse = await client.PostAsJsonAsync("/api/v1/children", new CreateChildRequest("ReportExportChild", 4, "4444"));
        Assert.Equal(HttpStatusCode.Created, childResponse.StatusCode);
        var child = await childResponse.Content.ReadFromJsonAsync<CreatedChildResponse>();
        Assert.NotNull(child);

        var lessonResponse = await client.PostAsJsonAsync("/api/v1/lessons", BuildLessonRequest("Report Export Lesson"));
        Assert.Equal(HttpStatusCode.Created, lessonResponse.StatusCode);
        var lesson = await lessonResponse.Content.ReadFromJsonAsync<LessonSummaryResponse>();
        Assert.NotNull(lesson);

        var assignmentResponse = await client.PostAsJsonAsync("/api/v1/assignments", new CreateAssignmentRequest(child!.Child.Id, lesson!.Id, DateTime.UtcNow.AddDays(2)));
        Assert.Equal(HttpStatusCode.Created, assignmentResponse.StatusCode);
        var assignment = await assignmentResponse.Content.ReadFromJsonAsync<AssignmentResponse>();
        Assert.NotNull(assignment);

        var exportResponse = await client.GetAsync($"/api/v1/reports/children/{child.Child.Id}/export?format=csv");
        Assert.Equal(HttpStatusCode.OK, exportResponse.StatusCode);
        Assert.Equal("text/csv", exportResponse.Content.Headers.ContentType?.MediaType);

        var csv = await exportResponse.Content.ReadAsStringAsync();
        Assert.Contains("assignmentId,assignedAt,dueDate,status,score,completedAt,correctAnswers,totalQuestions", csv);
        Assert.Contains(assignment!.Id.ToString(), csv);
        Assert.Contains(",Assigned,", csv);
    }

    private static CreateLessonRequest BuildLessonRequest(string title)
    {
        return new CreateLessonRequest(
            title,
            "Math",
            3,
            "Fractions",
            "Easy",
            new List<CreateQuestionRequest>
            {
                new(
                    "1/2 + 1/2 = ?",
                    "1",
                    1,
                    new List<CreateAnswerOptionRequest>
                    {
                        new("1", true, 1),
                        new("2", false, 2)
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
