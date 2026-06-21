using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Xunit;

public class AssignmentHandlerUnitTests
{
    [Fact]
    public async Task GetParentAssignmentsQueryHandler_ForwardsToReadService()
    {
        var expected = new List<AssignmentResponse>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), "Child A", Guid.NewGuid(), "Lesson A", "Math", DateTime.UtcNow, null, "Assigned", null, null, null)
        };

        var handler = new GetParentAssignmentsQueryHandler(new FakeAssignmentReadService(expected));

        var result = await handler.Handle(new GetParentAssignmentsQuery(Guid.NewGuid(), null), CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(expected[0].Id, result[0].Id);
    }

    [Fact]
    public async Task GetChildAssignmentsQueryHandler_ForwardsToReadService()
    {
        var expected = new List<AssignmentResponse>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), "Child B", Guid.NewGuid(), "Lesson B", "English", DateTime.UtcNow, null, "Completed", null, null, null)
        };

        var handler = new GetChildAssignmentsQueryHandler(new FakeAssignmentReadService(expected));

        var result = await handler.Handle(new GetChildAssignmentsQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(expected[0].Status, result[0].Status);
    }

    [Fact]
    public async Task AssignmentReadService_ListForParentAsync_ReturnsOnlyMatchingAssignments_InDescendingOrder()
    {
        await using var db = CreateDbContext();

        var parent = CreateUser("parent@example.com", UserRole.Parent);
        var otherParent = CreateUser("other@example.com", UserRole.Parent);
        var childA = CreateChild(parent, "Child A");
        var childB = CreateChild(parent, "Child B");
        var otherChild = CreateChild(otherParent, "Other Child");
        var lesson = CreateLesson(parent, "Lesson");

        db.Users.AddRange(parent, otherParent, childA.User!, childB.User!, otherChild.User!);
        db.Children.AddRange(childA, childB, otherChild);
        db.Lessons.Add(lesson);
        db.Assignments.AddRange(
            new Assignment { Child = childA, Lesson = lesson, AssignedAt = DateTime.UtcNow.AddHours(-3), Status = "Assigned" },
            new Assignment { Child = childB, Lesson = lesson, AssignedAt = DateTime.UtcNow.AddHours(-1), Status = "Completed" },
            new Assignment { Child = otherChild, Lesson = lesson, AssignedAt = DateTime.UtcNow, Status = "Assigned" });

        await db.SaveChangesAsync();

        var service = new AssignmentReadService(db);
        var allForParent = await service.ListForParentAsync(parent.Id, null);

        Assert.Equal(2, allForParent.Count);
        Assert.Equal(childB.Id, allForParent[0].ChildId);
        Assert.Equal(childA.Id, allForParent[1].ChildId);

        var filtered = await service.ListForParentAsync(parent.Id, childA.Id);
        Assert.Single(filtered);
        Assert.Equal(childA.Id, filtered[0].ChildId);
    }

    [Fact]
    public async Task AssignmentSolvingService_GetForSolvingAsync_ReturnsNotFound_WhenAssignmentIsNotOwnedByParent()
    {
        await using var db = CreateDbContext();

        var parent = CreateUser("parent@example.com", UserRole.Parent);
        var otherParent = CreateUser("other@example.com", UserRole.Parent);
        var child = CreateChild(otherParent, "Kid");
        var lesson = CreateLesson(otherParent, "Lesson");
        var assignment = CreateAssignment(child, lesson, DateTime.UtcNow);

        db.Users.AddRange(parent, otherParent, child.User!);
        db.Children.Add(child);
        db.Lessons.Add(lesson);
        db.Assignments.Add(assignment);
        await db.SaveChangesAsync();

        var service = new AssignmentSolvingService(db, new FakeEmailService());
        var result = await service.GetForSolvingAsync(AssignmentAccessScope.Parent, parent.Id, assignment.Id);

        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("Assignment not found.", result.Error);
    }

    [Fact]
    public async Task AssignmentSolvingService_GetForSolvingAsync_ReturnsNotFound_WhenChildDoesNotOwnAssignment()
    {
        await using var db = CreateDbContext();

        var parent = CreateUser("parent@example.com", UserRole.Parent);
        var child = CreateChild(parent, "Kid");
        var lesson = CreateLessonWithQuestion(parent, out _, out _, out _);
        var assignment = CreateAssignment(child, lesson, DateTime.UtcNow);

        db.Users.AddRange(parent, child.User!);
        db.Children.Add(child);
        db.Lessons.Add(lesson);
        db.Assignments.Add(assignment);
        await db.SaveChangesAsync();

        var service = new AssignmentSolvingService(db, new FakeEmailService());
        var result = await service.GetForSolvingAsync(AssignmentAccessScope.Child, Guid.NewGuid(), assignment.Id);

        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("Assignment not found.", result.Error);
    }

    [Fact]
    public async Task AssignmentSolvingService_SubmitAnswersAsync_UpdatesStatusAndReturnsPartialScore()
    {
        await using var db = CreateDbContext();

        var parent = CreateUser("parent@example.com", UserRole.Parent);
        var child = CreateChild(parent, "Kid");
        var lesson = CreateLessonWithQuestion(parent, out var question, out var correctOption, out _);
        var assignment = CreateAssignment(child, lesson, DateTime.UtcNow.AddDays(1));

        db.Users.AddRange(parent, child.User!);
        db.Children.Add(child);
        db.Lessons.Add(lesson);
        db.Assignments.Add(assignment);
        await db.SaveChangesAsync();

        var service = new AssignmentSolvingService(db, new FakeEmailService());
        var result = await service.SubmitAnswersAsync(
            AssignmentAccessScope.Parent,
            parent.Id,
            assignment.Id,
            new SubmitAssignmentAnswersRequest(new List<SubmitAnswerRequest>
            {
                new(question.Id, correctOption.Id, null)
            }));

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(100m, result.Value!.PartialScore);

        var updatedAssignment = await db.Assignments.SingleAsync(x => x.Id == assignment.Id);
        Assert.Equal("InProgress", updatedAssignment.Status);

        var storedAnswer = await db.AssignmentAnswers.SingleAsync(x => x.AssignmentId == assignment.Id);
        Assert.True(storedAnswer.IsCorrect);
    }

    [Fact]
    public async Task AssignmentSolvingService_SubmitAnswersAsync_ReturnsBadRequest_WhenAnswerQuestionDoesNotBelongToAssignment()
    {
        await using var db = CreateDbContext();

        var parent = CreateUser("parent@example.com", UserRole.Parent);
        var child = CreateChild(parent, "Kid");
        var lesson = CreateLessonWithQuestion(parent, out _, out _, out _);
        var assignment = CreateAssignment(child, lesson, DateTime.UtcNow.AddDays(1));
        var unrelatedLesson = CreateLesson(parent, "Other Lesson");
        var unrelatedQuestion = new Question
        {
            QuestionText = "Unrelated?",
            Explanation = "No",
            Order = 1,
            Lesson = unrelatedLesson
        };
        unrelatedLesson.Questions.Add(unrelatedQuestion);

        db.Users.AddRange(parent, child.User!);
        db.Children.Add(child);
        db.Lessons.AddRange(lesson, unrelatedLesson);
        db.Assignments.Add(assignment);
        await db.SaveChangesAsync();

        var service = new AssignmentSolvingService(db, new FakeEmailService());
        var result = await service.SubmitAnswersAsync(
            AssignmentAccessScope.Child,
            child.Id,
            assignment.Id,
            new SubmitAssignmentAnswersRequest(new List<SubmitAnswerRequest>
            {
                new(unrelatedQuestion.Id, null, "anything")
            }));

        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("Question does not belong to assignment lesson.", result.Error);
    }

    [Fact]
    public async Task AssignmentSolvingService_CompleteAsync_CreatesResult_AndGetResultAsync_ReturnsBreakdown()
    {
        await using var db = CreateDbContext();

        var parent = CreateUser("parent@example.com", UserRole.Parent);
        var child = CreateChild(parent, "Kid");
        var lesson = CreateLessonWithQuestion(parent, out var question, out var correctOption, out _);
        var assignment = CreateAssignment(child, lesson, DateTime.UtcNow.AddDays(1));

        db.Users.AddRange(parent, child.User!);
        db.Children.Add(child);
        db.Lessons.Add(lesson);
        db.Assignments.Add(assignment);
        await db.SaveChangesAsync();

        var service = new AssignmentSolvingService(db, new FakeEmailService());

        var submit = await service.SubmitAnswersAsync(
            AssignmentAccessScope.Parent,
            parent.Id,
            assignment.Id,
            new SubmitAssignmentAnswersRequest(new List<SubmitAnswerRequest>
            {
                new(question.Id, correctOption.Id, null)
            }));

        Assert.Equal(StatusCodes.Status200OK, submit.StatusCode);

        var complete = await service.CompleteAsync(AssignmentAccessScope.Parent, parent.Id, assignment.Id);
        Assert.Equal(StatusCodes.Status200OK, complete.StatusCode);
        Assert.NotNull(complete.Value);

        var result = await service.GetResultAsync(AssignmentAccessScope.Parent, parent.Id, complete.Value!.ResultId);
        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Single(result.Value!.Breakdown);
        Assert.True(result.Value.Breakdown[0].Correct);
    }

    [Fact]
    public async Task AssignmentSolvingService_GetResultAsync_ReturnsNotFound_WhenChildDoesNotOwnResult()
    {
        await using var db = CreateDbContext();

        var parent = CreateUser("parent@example.com", UserRole.Parent);
        var otherParent = CreateUser("other@example.com", UserRole.Parent);
        var child = CreateChild(otherParent, "Kid");
        var lesson = CreateLessonWithQuestion(otherParent, out var question, out var correctOption, out _);
        var assignment = CreateAssignment(child, lesson, DateTime.UtcNow);

        db.Users.AddRange(parent, otherParent, child.User!);
        db.Children.Add(child);
        db.Lessons.Add(lesson);
        db.Assignments.Add(assignment);
        await db.SaveChangesAsync();

        var service = new AssignmentSolvingService(db, new FakeEmailService());
        await service.SubmitAnswersAsync(
            AssignmentAccessScope.Child,
            child.Id,
            assignment.Id,
            new SubmitAssignmentAnswersRequest(new List<SubmitAnswerRequest>
            {
                new(question.Id, correctOption.Id, null)
            }));

        var complete = await service.CompleteAsync(AssignmentAccessScope.Child, child.Id, assignment.Id);
        Assert.Equal(StatusCodes.Status200OK, complete.StatusCode);

        var result = await service.GetResultAsync(AssignmentAccessScope.Child, Guid.NewGuid(), complete.Value!.ResultId);
        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("Result not found.", result.Error);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"assignment-handler-tests-{Guid.NewGuid():N}")
            .Options;

        return new AppDbContext(options);
    }

    private static AppUser CreateUser(string email, UserRole role)
    {
        return new AppUser
        {
            Email = email,
            PasswordHash = "hash",
            Role = role,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static Child CreateChild(AppUser parent, string name)
    {
        var user = CreateUser($"{name.ToLowerInvariant().Replace(" ", ".")}@example.com", UserRole.Child);
        return new Child
        {
            Parent = parent,
            ParentId = parent.Id,
            User = user,
            UserId = user.Id,
            Name = name,
            Grade = 3
        };
    }

    private static Lesson CreateLesson(AppUser parent, string title)
    {
        var lesson = new Lesson
        {
            Title = title,
            Subject = "Math",
            Grade = 3,
            Topic = "Addition",
            Difficulty = "Easy",
            CreatedBy = parent.Id,
            CreatedAt = DateTime.UtcNow
        };

        return lesson;
    }

    private static Lesson CreateLessonWithQuestion(AppUser parent, out Question question, out AnswerOption correctOption, out AnswerOption wrongOption)
    {
        var lesson = CreateLesson(parent, "Lesson With Question");
        question = new Question
        {
            QuestionText = "2 + 2 = ?",
            Explanation = "Basic addition",
            Order = 1
        };

        correctOption = new AnswerOption
        {
            AnswerText = "4",
            IsCorrect = true,
            Order = 1
        };

        wrongOption = new AnswerOption
        {
            AnswerText = "5",
            IsCorrect = false,
            Order = 2
        };

        question.Answers.Add(correctOption);
        question.Answers.Add(wrongOption);
        lesson.Questions.Add(question);
        return lesson;
    }

    private static Assignment CreateAssignment(Child child, Lesson lesson, DateTime assignedAt)
    {
        return new Assignment
        {
            Child = child,
            ChildId = child.Id,
            Lesson = lesson,
            LessonId = lesson.Id,
            AssignedAt = assignedAt,
            Status = "Assigned"
        };
    }

    private sealed class FakeAssignmentReadService : IAssignmentReadService
    {
        private readonly IReadOnlyList<AssignmentResponse> _response;

        public FakeAssignmentReadService(IReadOnlyList<AssignmentResponse> response)
        {
            _response = response;
        }

        public Task<IReadOnlyList<AssignmentResponse>> ListForParentAsync(Guid parentId, Guid? childId)
        {
            return Task.FromResult(_response);
        }

        public Task<IReadOnlyList<AssignmentResponse>> ListForChildAsync(Guid childId)
        {
            return Task.FromResult(_response);
        }
    }

    private sealed class FakeEmailService : IEmailService
    {
        public Task<bool> SendInvitationAsync(string toEmail, string? displayName, string inviterName)
            => Task.FromResult(true);

        public Task<bool> SendParentLinkedAsync(string toEmail, string? displayName, string linkedByEmail)
            => Task.FromResult(true);

        public Task<bool> SendFriendInviteAsync(string toEmail, string inviterName, string inviteUrl)
            => Task.FromResult(true);

        public Task<bool> SendAssignmentCompletedToParentAsync(string toEmail, string parentName, string childName, string lessonTitle, decimal score, int correctAnswers, int totalQuestions, IList<(string LessonTitle, decimal Score)> recentResults)
            => Task.FromResult(true);

        public Task<bool> SendAssignmentCreatedToChildAsync(string toEmail, string childName, string lessonTitle, string subject, DateTime? dueDate)
            => Task.FromResult(true);
    }
}