public sealed record ChildReportSummaryResponse(
    Guid ChildId,
    decimal CompletionRate,
    decimal AverageScore,
    int SolvedCount,
    int StreakDays);
