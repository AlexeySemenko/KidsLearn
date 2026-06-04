using MediatR;
using Microsoft.EntityFrameworkCore;

public sealed record GetParentChildReportSummaryQuery(Guid ParentId, Guid ChildId, DateTime? From, DateTime? To)
    : IRequest<GetParentChildReportSummaryResult>;

public sealed record GetParentChildReportSummaryResult(ChildReportSummaryResponse? Response, string? Error, int StatusCode)
{
    public static GetParentChildReportSummaryResult NotFound(string error)
        => new(null, error, StatusCodes.Status404NotFound);

    public static GetParentChildReportSummaryResult Success(ChildReportSummaryResponse response)
        => new(response, null, StatusCodes.Status200OK);
}

public sealed class GetParentChildReportSummaryQueryHandler
    : IRequestHandler<GetParentChildReportSummaryQuery, GetParentChildReportSummaryResult>
{
    private readonly AppDbContext _db;

    public GetParentChildReportSummaryQueryHandler(AppDbContext db)
    {
        _db = db;
    }

    public async Task<GetParentChildReportSummaryResult> Handle(GetParentChildReportSummaryQuery query, CancellationToken cancellationToken)
    {
        var childBelongsToParent = await ApiEndpointHelpers.EnsureParentOwnsChildAsync(_db, query.ParentId, query.ChildId);
        if (!childBelongsToParent)
        {
            return GetParentChildReportSummaryResult.NotFound("Child not found.");
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

        var assignments = await assignmentsQuery
            .Include(x => x.Result)
            .ToListAsync(cancellationToken);

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

        return GetParentChildReportSummaryResult.Success(new ChildReportSummaryResponse(
            query.ChildId,
            completionRate,
            averageScore,
            solvedCount,
            streakDays));
    }
}