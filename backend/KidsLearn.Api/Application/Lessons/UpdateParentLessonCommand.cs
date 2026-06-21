using MediatR;
using Microsoft.EntityFrameworkCore;

public sealed record UpdateParentLessonCommand(Guid ParentId, Guid LessonId, UpdateLessonRequest Request)
    : IRequest<UpdateParentLessonResult>, IValidationFailureResponseFactory<UpdateParentLessonResult>
{
    public UpdateParentLessonResult CreateValidationFailureResponse(string error)
    {
        return UpdateParentLessonResult.BadRequest(error);
    }
}

public sealed record UpdateParentLessonResult(LessonSummaryResponse? Lesson, string? Error, int StatusCode)
{
    public static UpdateParentLessonResult BadRequest(string error) => new(null, error, StatusCodes.Status400BadRequest);

    public static UpdateParentLessonResult NotFound(string error) => new(null, error, StatusCodes.Status404NotFound);

    public static UpdateParentLessonResult Ok(LessonSummaryResponse lesson) => new(lesson, null, StatusCodes.Status200OK);
}

public sealed class UpdateParentLessonCommandHandler : IRequestHandler<UpdateParentLessonCommand, UpdateParentLessonResult>
{
    private readonly AppDbContext _db;

    public UpdateParentLessonCommandHandler(AppDbContext db)
    {
        _db = db;
    }

    public async Task<UpdateParentLessonResult> Handle(UpdateParentLessonCommand command, CancellationToken cancellationToken)
    {
        var scopedParentIds = await ApiEndpointHelpers.ResolveParentScopeIdsAsync(_db, command.ParentId);

        var lesson = await _db.Lessons.FirstOrDefaultAsync(
            x => x.Id == command.LessonId && scopedParentIds.Contains(x.CreatedBy),
            cancellationToken);

        if (lesson is null)
        {
            return UpdateParentLessonResult.NotFound("Lesson not found.");
        }

        var request = command.Request;

        if (request.Title is not null)
        {
            lesson.Title = request.Title.Trim();
        }

        if (request.Subject is not null)
        {
            lesson.Subject = request.Subject.Trim();
        }

        if (request.Topic is not null)
        {
            lesson.Topic = request.Topic.Trim();
        }

        if (request.Difficulty is not null)
        {
            lesson.Difficulty = request.Difficulty.Trim();
        }

        if (request.Grade.HasValue)
        {
            lesson.Grade = request.Grade.Value;
        }

        if (request.Story is not null)
        {
            lesson.Story = string.IsNullOrWhiteSpace(request.Story) ? null : request.Story.Trim();
        }

        await _db.SaveChangesAsync(cancellationToken);

        var response = new LessonSummaryResponse(
            lesson.Id,
            lesson.Title,
            lesson.Subject,
            lesson.Grade,
            lesson.Topic,
            lesson.Difficulty,
            lesson.CreatedAt,
            await _db.Questions.CountAsync(x => x.LessonId == lesson.Id, cancellationToken),
            null);

        return UpdateParentLessonResult.Ok(response);
    }
}