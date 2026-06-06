using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public class AssignmentSolvingIntegrationTests
{
    [Fact]
    public async Task Parent_SolvingFlow_Works_EndToEnd()
    {
        await using var factory = new KidsLearnApiFactory();
        using var client = factory.CreateClient();
        EnsureParentUser(factory.Services);

        var parentToken = CreateParentToken(factory.Services);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", parentToken);

        var childResponse = await client.PostAsJsonAsync("/api/v1/children", new CreateChildRequest("ParentFlowChild", 2, "1111"));
        Assert.Equal(HttpStatusCode.Created, childResponse.StatusCode);
        var createdChild = await childResponse.Content.ReadFromJsonAsync<CreatedChildResponse>();
        Assert.NotNull(createdChild);

        var lessonResponse = await client.PostAsJsonAsync("/api/v1/lessons", BuildLessonRequest("Parent Flow Lesson"));
        Assert.Equal(HttpStatusCode.Created, lessonResponse.StatusCode);
        var lesson = await lessonResponse.Content.ReadFromJsonAsync<LessonSummaryResponse>();
        Assert.NotNull(lesson);

        var assignmentResponse = await client.PostAsJsonAsync("/api/v1/assignments", new CreateAssignmentRequest(createdChild!.Child.Id, lesson!.Id, DateTime.UtcNow.AddDays(1)));
        Assert.Equal(HttpStatusCode.Created, assignmentResponse.StatusCode);
        var assignment = await assignmentResponse.Content.ReadFromJsonAsync<AssignmentResponse>();
        Assert.NotNull(assignment);

        var solvingResponse = await client.GetAsync($"/api/v1/assignments/{assignment!.Id}/for-solving");
        Assert.Equal(HttpStatusCode.OK, solvingResponse.StatusCode);
        var solving = await solvingResponse.Content.ReadFromJsonAsync<AssignmentForSolvingResponse>();
        Assert.NotNull(solving);
        Assert.Single(solving!.Questions);

        var question = solving.Questions[0];
        var correctOption = question.Answers.First();
        var submitRequest = new SubmitAssignmentAnswersRequest(
            new List<SubmitAnswerRequest> { new(question.QuestionId, correctOption.AnswerId, null) });

        var submitResponse = await client.PostAsJsonAsync($"/api/v1/assignments/{assignment.Id}/answers", submitRequest);
        Assert.Equal(HttpStatusCode.OK, submitResponse.StatusCode);
        var submit = await submitResponse.Content.ReadFromJsonAsync<SubmitAssignmentAnswersResponse>();
        Assert.NotNull(submit);
        Assert.Equal(100m, submit!.PartialScore);

        var completeResponse = await client.PostAsync($"/api/v1/assignments/{assignment.Id}/complete", null);
        Assert.Equal(HttpStatusCode.OK, completeResponse.StatusCode);
        var complete = await completeResponse.Content.ReadFromJsonAsync<CompleteAssignmentResponse>();
        Assert.NotNull(complete);
        Assert.Equal(100m, complete!.Score);

        var resultResponse = await client.GetAsync($"/api/v1/results/{complete.ResultId}");
        Assert.Equal(HttpStatusCode.OK, resultResponse.StatusCode);
        var result = await resultResponse.Content.ReadFromJsonAsync<ResultDetailResponse>();
        Assert.NotNull(result);
        Assert.Single(result!.Breakdown);
        Assert.True(result.Breakdown[0].Correct);
    }

    [Fact]
    public async Task Child_SolvingFlow_Works_EndToEnd()
    {
        await using var factory = new KidsLearnApiFactory();
        using var client = factory.CreateClient();
        EnsureParentUser(factory.Services);

        var parentToken = CreateParentToken(factory.Services);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", parentToken);

        var childResponse = await client.PostAsJsonAsync("/api/v1/children", new CreateChildRequest("ChildFlowChild", 3, "2222"));
        Assert.Equal(HttpStatusCode.Created, childResponse.StatusCode);
        var createdChild = await childResponse.Content.ReadFromJsonAsync<CreatedChildResponse>();
        Assert.NotNull(createdChild);

        var lessonResponse = await client.PostAsJsonAsync("/api/v1/lessons", BuildLessonRequest("Child Flow Lesson"));
        Assert.Equal(HttpStatusCode.Created, lessonResponse.StatusCode);
        var lesson = await lessonResponse.Content.ReadFromJsonAsync<LessonSummaryResponse>();
        Assert.NotNull(lesson);

        var assignmentResponse = await client.PostAsJsonAsync("/api/v1/assignments", new CreateAssignmentRequest(createdChild!.Child.Id, lesson!.Id, DateTime.UtcNow.AddDays(1)));
        Assert.Equal(HttpStatusCode.Created, assignmentResponse.StatusCode);
        var assignment = await assignmentResponse.Content.ReadFromJsonAsync<AssignmentResponse>();
        Assert.NotNull(assignment);

        var childToken = CreateChildToken(factory.Services, createdChild.Child.Id);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", childToken);

        var solvingResponse = await client.GetAsync($"/api/v1/child/assignments/{assignment!.Id}/for-solving");
        Assert.Equal(HttpStatusCode.OK, solvingResponse.StatusCode);
        var solving = await solvingResponse.Content.ReadFromJsonAsync<AssignmentForSolvingResponse>();
        Assert.NotNull(solving);
        Assert.Single(solving!.Questions);

        var question = solving.Questions[0];
        var correctOption = question.Answers.First();
        var submitRequest = new SubmitAssignmentAnswersRequest(
            new List<SubmitAnswerRequest> { new(question.QuestionId, correctOption.AnswerId, null) });

        var submitResponse = await client.PostAsJsonAsync($"/api/v1/child/assignments/{assignment.Id}/answers", submitRequest);
        Assert.Equal(HttpStatusCode.OK, submitResponse.StatusCode);
        var submit = await submitResponse.Content.ReadFromJsonAsync<SubmitAssignmentAnswersResponse>();
        Assert.NotNull(submit);
        Assert.Equal(100m, submit!.PartialScore);

        var completeResponse = await client.PostAsync($"/api/v1/child/assignments/{assignment.Id}/complete", null);
        Assert.Equal(HttpStatusCode.OK, completeResponse.StatusCode);
        var complete = await completeResponse.Content.ReadFromJsonAsync<CompleteAssignmentResponse>();
        Assert.NotNull(complete);
        Assert.Equal(100m, complete!.Score);

        var resultResponse = await client.GetAsync($"/api/v1/child/results/{complete.ResultId}");
        Assert.Equal(HttpStatusCode.OK, resultResponse.StatusCode);
        var result = await resultResponse.Content.ReadFromJsonAsync<ResultDetailResponse>();
        Assert.NotNull(result);
        Assert.Single(result!.Breakdown);
        Assert.True(result.Breakdown[0].Correct);
    }

    private static CreateLessonRequest BuildLessonRequest(string title)
    {
        return new CreateLessonRequest(
            title,
            "Math",
            2,
            "Subtraction",
            "Easy",
            new List<CreateQuestionRequest>
            {
                new(
                    "9-4=?",
                    "5",
                    1,
                    new List<CreateAnswerOptionRequest>
                    {
                        new("5", true, 1),
                        new("6", false, 2)
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

    private static string CreateChildToken(IServiceProvider services, Guid childId)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tokenService = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();

        var childUser = db.Children
            .AsNoTracking()
            .Where(x => x.Id == childId && x.UserId.HasValue)
            .Join(db.Users, child => child.UserId, user => user.Id, (_, user) => user)
            .First();

        return tokenService.CreateAccessToken(childUser);
    }
}
