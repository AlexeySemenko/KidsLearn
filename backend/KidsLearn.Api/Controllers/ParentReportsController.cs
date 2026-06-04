using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;

public static class ParentReportsController
{
    public static RouteGroupBuilder MapParentReportsEndpoints(this RouteGroupBuilder parentApi)
    {
        parentApi.MapGet("/reports/children/{childId:guid}", async (AppDbContext db, ClaimsPrincipal user, Guid childId, DateTime? from, DateTime? to) =>
        {
            if (!ApiEndpointHelpers.TryResolveUserId(user, out var parentId))
            {
                return Results.Unauthorized();
            }

            var childBelongsToParent = await ApiEndpointHelpers.EnsureParentOwnsChildAsync(db, parentId, childId);
            if (!childBelongsToParent)
            {
                return Results.NotFound(new { error = "Child not found." });
            }

            var assignmentsQuery = db.Assignments
                .AsNoTracking()
                .Where(x => x.ChildId == childId);

            if (from.HasValue)
            {
                assignmentsQuery = assignmentsQuery.Where(x => x.AssignedAt >= from.Value);
            }

            if (to.HasValue)
            {
                assignmentsQuery = assignmentsQuery.Where(x => x.AssignedAt <= to.Value);
            }

            var assignments = await assignmentsQuery
                .Include(x => x.Result)
                .ToListAsync();

            var totalCount = assignments.Count;
            var solvedAssignments = assignments.Where(x => x.Result is not null).ToList();
            var solvedCount = solvedAssignments.Count;

            var completionRate = totalCount == 0
                ? 0m
                : Math.Round(100m * solvedCount / totalCount, 2);

            var averageScore = solvedCount == 0
                ? 0m
                : Math.Round(solvedAssignments.Average(x => x.Result!.Score), 2);

            var completionDays = solvedAssignments
                .Select(x => x.Result!.CompletedAt.Date)
                .Distinct()
                .OrderByDescending(x => x)
                .ToList();

            var streakDays = 0;
            DateTime? expectedDay = null;
            foreach (var day in completionDays)
            {
                if (!expectedDay.HasValue)
                {
                    streakDays = 1;
                    expectedDay = day.AddDays(-1);
                    continue;
                }

                if (day == expectedDay.Value)
                {
                    streakDays++;
                    expectedDay = day.AddDays(-1);
                    continue;
                }

                break;
            }

            return Results.Ok(new ChildReportSummaryResponse(
                childId,
                completionRate,
                averageScore,
                solvedCount,
                streakDays));
        });

        parentApi.MapGet("/reports/children/{childId:guid}/export", async (AppDbContext db, ClaimsPrincipal user, Guid childId, string? format, DateTime? from, DateTime? to) =>
        {
            if (!ApiEndpointHelpers.TryResolveUserId(user, out var parentId))
            {
                return Results.Unauthorized();
            }

            var childBelongsToParent = await ApiEndpointHelpers.EnsureParentOwnsChildAsync(db, parentId, childId);
            if (!childBelongsToParent)
            {
                return Results.NotFound(new { error = "Child not found." });
            }

            if (!string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
            {
                return Results.BadRequest(new { error = "Only format=csv is supported." });
            }

            var assignmentsQuery = db.Assignments
                .AsNoTracking()
                .Where(x => x.ChildId == childId);

            if (from.HasValue)
            {
                assignmentsQuery = assignmentsQuery.Where(x => x.AssignedAt >= from.Value);
            }

            if (to.HasValue)
            {
                assignmentsQuery = assignmentsQuery.Where(x => x.AssignedAt <= to.Value);
            }

            var rows = await assignmentsQuery
                .Include(x => x.Result)
                .OrderBy(x => x.AssignedAt)
                .ToListAsync();

            static string EscapeCsv(string value)
            {
                var needsQuotes = value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r');
                if (!needsQuotes)
                {
                    return value;
                }

                return $"\"{value.Replace("\"", "\"\"")}\"";
            }

            var sb = new StringBuilder();
            sb.AppendLine("assignmentId,assignedAt,dueDate,status,score,completedAt,correctAnswers,totalQuestions");

            foreach (var row in rows)
            {
                var score = row.Result is null ? string.Empty : row.Result.Score.ToString("0.##");
                var completedAt = row.Result?.CompletedAt.ToString("O") ?? string.Empty;
                var correctAnswers = row.Result?.CorrectAnswers.ToString() ?? string.Empty;
                var totalQuestions = row.Result?.TotalQuestions.ToString() ?? string.Empty;

                sb.AppendLine(string.Join(",",
                    row.Id,
                    row.AssignedAt.ToString("O"),
                    row.DueDate?.ToString("O") ?? string.Empty,
                    EscapeCsv(row.Status),
                    score,
                    completedAt,
                    correctAnswers,
                    totalQuestions));
            }

            var fileName = $"child-report-{childId}-{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            return Results.File(bytes, "text/csv", fileName);
        });

        return parentApi;
    }
}


