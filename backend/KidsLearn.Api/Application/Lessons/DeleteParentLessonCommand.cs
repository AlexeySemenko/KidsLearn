using MediatR;
using Microsoft.EntityFrameworkCore;

public sealed record DeleteParentLessonCommand(Guid ParentId, Guid LessonId)
    : IRequest<DeleteParentLessonResult>, IValidationFailureResponseFactory<DeleteParentLessonResult>
{
    public DeleteParentLessonResult CreateValidationFailureResponse(string error)
    {
        return DeleteParentLessonResult.BadRequest(error);
    }
}

public sealed record DeleteParentLessonResult(string? Error, int StatusCode)
{
    public static DeleteParentLessonResult BadRequest(string error) => new(error, StatusCodes.Status400BadRequest);

    public static DeleteParentLessonResult NotFound(string error) => new(error, StatusCodes.Status404NotFound);

    public static DeleteParentLessonResult Conflict(string error) => new(error, StatusCodes.Status409Conflict);

    public static DeleteParentLessonResult NoContent() => new(null, StatusCodes.Status204NoContent);
}

public sealed class DeleteParentLessonCommandHandler : IRequestHandler<DeleteParentLessonCommand, DeleteParentLessonResult>
{
    private readonly AppDbContext _db;

    public DeleteParentLessonCommandHandler(AppDbContext db)
    {
        _db = db;
    }

    public async Task<DeleteParentLessonResult> Handle(DeleteParentLessonCommand command, CancellationToken cancellationToken)
    {
        var lesson = await _db.Lessons.FirstOrDefaultAsync(
            x => x.Id == command.LessonId && x.CreatedBy == command.ParentId,
            cancellationToken);

        if (lesson is null)
        {
            return DeleteParentLessonResult.NotFound("Lesson not found.");
        }

        var hasAssignments = await _db.Assignments.AnyAsync(x => x.LessonId == lesson.Id, cancellationToken);
        if (hasAssignments)
        {
            return DeleteParentLessonResult.Conflict("Cannot delete a lesson with assignments.");
        }

        _db.Lessons.Remove(lesson);
        await _db.SaveChangesAsync(cancellationToken);
        return DeleteParentLessonResult.NoContent();
    }
}