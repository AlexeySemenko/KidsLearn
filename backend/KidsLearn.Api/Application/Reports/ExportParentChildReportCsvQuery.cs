using System.Text;
using MediatR;
using Microsoft.EntityFrameworkCore;

public sealed record ExportParentChildReportCsvQuery(Guid ParentId, Guid ChildId, string? Format, DateTime? From, DateTime? To)
    : IRequest<ExportParentChildReportCsvResult>;

public sealed record ExportParentChildReportCsvResult(byte[]? FileBytes, string? FileName, string? Error, int StatusCode)
{
    public static ExportParentChildReportCsvResult NotFound(string error)
        => new(null, null, error, StatusCodes.Status404NotFound);

    public static ExportParentChildReportCsvResult BadRequest(string error)
        => new(null, null, error, StatusCodes.Status400BadRequest);

    public static ExportParentChildReportCsvResult Success(byte[] fileBytes, string fileName)
        => new(fileBytes, fileName, null, StatusCodes.Status200OK);
}

public sealed class ExportParentChildReportCsvQueryHandler
    : IRequestHandler<ExportParentChildReportCsvQuery, ExportParentChildReportCsvResult>
{
    private readonly AppDbContext _db;

    public ExportParentChildReportCsvQueryHandler(AppDbContext db)
    {
        _db = db;
    }

    public async Task<ExportParentChildReportCsvResult> Handle(ExportParentChildReportCsvQuery query, CancellationToken cancellationToken)
    {
        var childBelongsToParent = await ApiEndpointHelpers.EnsureParentOwnsChildAsync(_db, query.ParentId, query.ChildId);
        if (!childBelongsToParent)
        {
            return ExportParentChildReportCsvResult.NotFound("Child not found.");
        }

        if (!string.Equals(query.Format, "csv", StringComparison.OrdinalIgnoreCase))
        {
            return ExportParentChildReportCsvResult.BadRequest("Only format=csv is supported.");
        }

        var assignmentsQuery = _db.Assignments
            .AsNoTracking()
            .Where(x => x.ChildId == query.ChildId);

        if (query.From.HasValue)
        {
            assignmentsQuery = assignmentsQuery.Where(x => x.AssignedAt >= query.From.Value);
        }

        if (query.To.HasValue)
        {
            assignmentsQuery = assignmentsQuery.Where(x => x.AssignedAt <= query.To.Value);
        }

        var rows = await assignmentsQuery
            .Include(x => x.Result)
            .OrderBy(x => x.AssignedAt)
            .ToListAsync(cancellationToken);

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

        var fileName = $"child-report-{query.ChildId}-{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return ExportParentChildReportCsvResult.Success(bytes, fileName);
    }
}