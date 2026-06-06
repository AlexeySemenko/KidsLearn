public sealed record CreateAssignmentRequest(Guid ChildId, Guid LessonId, DateTime? DueDate);

public sealed record AssignmentResponse(
    Guid Id,
    Guid ChildId,
    Guid LessonId,
    DateTime AssignedAt,
    DateTime? DueDate,
    string Status);
