using MediatR;
using Microsoft.EntityFrameworkCore;

public sealed record SelfAssignLessonCommand(Guid ChildId, Guid LessonId) : IRequest<SelfAssignLessonResult>;

public sealed record SelfAssignLessonResult(AssignmentResponse? Assignment, string? Error, int StatusCode)
{
    public static SelfAssignLessonResult NotFound(string error) => new(null, error, StatusCodes.Status404NotFound);
    public static SelfAssignLessonResult Ok(AssignmentResponse assignment) => new(assignment, null, StatusCodes.Status200OK);
    public static SelfAssignLessonResult Created(AssignmentResponse assignment) => new(assignment, null, StatusCodes.Status201Created);
}

public sealed class SelfAssignLessonCommandHandler(AppDbContext db)
    : IRequestHandler<SelfAssignLessonCommand, SelfAssignLessonResult>
{
    public async Task<SelfAssignLessonResult> Handle(SelfAssignLessonCommand command, CancellationToken cancellationToken)
    {
        var lesson = await db.Lessons
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == command.LessonId, cancellationToken);

        if (lesson is null)
            return SelfAssignLessonResult.NotFound("Lesson not found.");

        var child = await db.Children
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == command.ChildId, cancellationToken);

        if (child is null)
            return SelfAssignLessonResult.NotFound("Child not found.");

        // Return existing active assignment if one already exists
        var existing = await db.Assignments
            .AsNoTracking()
            .FirstOrDefaultAsync(
                a => a.ChildId == command.ChildId && a.LessonId == command.LessonId && a.Status != "Completed",
                cancellationToken);

        if (existing is not null)
        {
            return SelfAssignLessonResult.Ok(new AssignmentResponse(
                existing.Id,
                existing.ChildId,
                child.Name,
                existing.LessonId,
                lesson.Title,
                lesson.Subject,
                existing.AssignedAt,
                existing.DueDate,
                existing.Status,
                null,
                null,
                null));
        }

        var assignment = new Assignment
        {
            ChildId = command.ChildId,
            LessonId = command.LessonId,
            AssignedAt = DateTime.UtcNow,
            Status = "Assigned",
        };

        db.Assignments.Add(assignment);
        await db.SaveChangesAsync(cancellationToken);

        return SelfAssignLessonResult.Created(new AssignmentResponse(
            assignment.Id,
            command.ChildId,
            child.Name,
            command.LessonId,
            lesson.Title,
            lesson.Subject,
            assignment.AssignedAt,
            null,
            "Assigned",
            null,
            null,
            null));
    }
}
