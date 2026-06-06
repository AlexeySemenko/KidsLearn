using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Xunit;

public class ReportsQueryHandlersUnitTests
{
    [Fact]
    public async Task GetParentChildReportSummaryQuery_ComputesMetrics_WhenChildBelongsToParent()
    {
        await using var db = CreateDbContext();

        var parent = CreateUser("parent@example.com", UserRole.Parent);
        var childUser = CreateUser("child@example.com", UserRole.Child);
        var child = new Child
        {
            ParentId = parent.Id,
            UserId = childUser.Id,
            Parent = parent,
            User = childUser,
            Name = "Kid",
            Grade = 3
        };

        var lesson = new Lesson
        {
            Title = "Math",
            Subject = "Math",
            Grade = 3,
            Topic = "Addition",
            Difficulty = "Easy",
            CreatedBy = parent.Id,
            CreatedAt = DateTime.UtcNow.AddDays(-10)
        };

        db.Users.AddRange(parent, childUser);
        db.Children.Add(child);
        db.Lessons.Add(lesson);

        var assignment1 = new Assignment
        {
            Child = child,
            Lesson = lesson,
            AssignedAt = DateTime.UtcNow.AddDays(-3),
            Status = "Completed",
            Result = new AssignmentResult
            {
                Score = 80m,
                CorrectAnswers = 8,
                TotalQuestions = 10,
                CompletedAt = DateTime.UtcNow.AddDays(-1)
            }
        };

        var assignment2 = new Assignment
        {
            Child = child,
            Lesson = lesson,
            AssignedAt = DateTime.UtcNow.AddDays(-2),
            Status = "Completed",
            Result = new AssignmentResult
            {
                Score = 60m,
                CorrectAnswers = 6,
                TotalQuestions = 10,
                CompletedAt = DateTime.UtcNow
            }
        };

        var assignment3 = new Assignment
        {
            Child = child,
            Lesson = lesson,
            AssignedAt = DateTime.UtcNow.AddDays(-1),
            Status = "Assigned"
        };

        db.Assignments.AddRange(assignment1, assignment2, assignment3);
        await db.SaveChangesAsync();

        var handler = new GetParentChildReportSummaryQueryHandler(db);
        var result = await handler.Handle(
            new GetParentChildReportSummaryQuery(parent.Id, child.Id, null, null),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.NotNull(result.Response);
        Assert.Equal(child.Id, result.Response!.ChildId);
        Assert.Equal(66.67m, result.Response.CompletionRate);
        Assert.Equal(70m, result.Response.AverageScore);
        Assert.Equal(2, result.Response.SolvedCount);
        Assert.Equal(2, result.Response.StreakDays);
    }

    [Fact]
    public async Task GetParentChildReportSummaryQuery_ReturnsNotFound_WhenChildNotOwned()
    {
        await using var db = CreateDbContext();

        var parentA = CreateUser("parent-a@example.com", UserRole.Parent);
        var parentB = CreateUser("parent-b@example.com", UserRole.Parent);
        var childUser = CreateUser("child@example.com", UserRole.Child);
        var child = new Child
        {
            ParentId = parentB.Id,
            UserId = childUser.Id,
            Parent = parentB,
            User = childUser,
            Name = "Kid",
            Grade = 4
        };

        db.Users.AddRange(parentA, parentB, childUser);
        db.Children.Add(child);
        await db.SaveChangesAsync();

        var handler = new GetParentChildReportSummaryQueryHandler(db);
        var result = await handler.Handle(
            new GetParentChildReportSummaryQuery(parentA.Id, child.Id, null, null),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal("Child not found.", result.Error);
    }

    [Fact]
    public async Task ExportParentChildReportCsvQuery_ReturnsBadRequest_WhenFormatIsNotCsv()
    {
        await using var db = CreateDbContext();

        var parent = CreateUser("parent@example.com", UserRole.Parent);
        var childUser = CreateUser("child@example.com", UserRole.Child);
        var child = new Child
        {
            ParentId = parent.Id,
            UserId = childUser.Id,
            Parent = parent,
            User = childUser,
            Name = "Kid",
            Grade = 2
        };

        db.Users.AddRange(parent, childUser);
        db.Children.Add(child);
        await db.SaveChangesAsync();

        var handler = new ExportParentChildReportCsvQueryHandler(db);
        var result = await handler.Handle(
            new ExportParentChildReportCsvQuery(parent.Id, child.Id, "json", null, null),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("Only format=csv is supported.", result.Error);
    }

    [Fact]
    public async Task ExportParentChildReportCsvQuery_ReturnsCsv_WithHeaderAndRows()
    {
        await using var db = CreateDbContext();

        var parent = CreateUser("parent@example.com", UserRole.Parent);
        var childUser = CreateUser("child@example.com", UserRole.Child);
        var child = new Child
        {
            ParentId = parent.Id,
            UserId = childUser.Id,
            Parent = parent,
            User = childUser,
            Name = "Kid",
            Grade = 3
        };

        var lesson = new Lesson
        {
            Title = "Science",
            Subject = "Science",
            Grade = 3,
            Topic = "Plants",
            Difficulty = "Medium",
            CreatedBy = parent.Id,
            CreatedAt = DateTime.UtcNow
        };

        db.Users.AddRange(parent, childUser);
        db.Children.Add(child);
        db.Lessons.Add(lesson);

        db.Assignments.Add(new Assignment
        {
            Child = child,
            Lesson = lesson,
            AssignedAt = DateTime.UtcNow.AddDays(-2),
            Status = "Completed",
            Result = new AssignmentResult
            {
                Score = 75m,
                CorrectAnswers = 3,
                TotalQuestions = 4,
                CompletedAt = DateTime.UtcNow.AddDays(-1)
            }
        });

        await db.SaveChangesAsync();

        var handler = new ExportParentChildReportCsvQueryHandler(db);
        var result = await handler.Handle(
            new ExportParentChildReportCsvQuery(parent.Id, child.Id, "csv", null, null),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.NotNull(result.FileBytes);
        Assert.NotNull(result.FileName);
        Assert.StartsWith($"child-report-{child.Id}", result.FileName!);

        var csv = Encoding.UTF8.GetString(result.FileBytes!);
        Assert.Contains("assignmentId,assignedAt,dueDate,status,score,completedAt,correctAnswers,totalQuestions", csv);
        Assert.Contains("Completed", csv);
        Assert.Contains("75", csv);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"reports-handler-tests-{Guid.NewGuid():N}")
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
}
