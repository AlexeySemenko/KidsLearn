using MediatR;
using Microsoft.EntityFrameworkCore;

public sealed record CreateParentAssignmentCommand(Guid ParentId, CreateAssignmentRequest Request)
    : IRequest<CreateParentAssignmentResult>, IValidationFailureResponseFactory<CreateParentAssignmentResult>
{
    public CreateParentAssignmentResult CreateValidationFailureResponse(string error)
    {
        return CreateParentAssignmentResult.BadRequest(error);
    }
}

public sealed record CreateParentAssignmentResult(AssignmentResponse? Assignment, string? Error, int StatusCode)
{
    public static CreateParentAssignmentResult BadRequest(string error) => new(null, error, StatusCodes.Status400BadRequest);

    public static CreateParentAssignmentResult Created(AssignmentResponse assignment) => new(assignment, null, StatusCodes.Status201Created);
}

public sealed class CreateParentAssignmentCommandHandler : IRequestHandler<CreateParentAssignmentCommand, CreateParentAssignmentResult>
{
    private readonly AppDbContext _db;

    public CreateParentAssignmentCommandHandler(AppDbContext db)
    {
        _db = db;
    }

    public async Task<CreateParentAssignmentResult> Handle(CreateParentAssignmentCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;
        var scopedParentIds = await ApiEndpointHelpers.ResolveParentScopeIdsAsync(_db, command.ParentId);

        var childBelongsToParent = await ApiEndpointHelpers.EnsureParentOwnsChildAsync(_db, command.ParentId, request.ChildId);
        if (!childBelongsToParent)
        {
            return CreateParentAssignmentResult.BadRequest("Child does not belong to current parent.");
        }

        var lesson = await _db.Lessons.FirstOrDefaultAsync(
            x => x.Id == request.LessonId && scopedParentIds.Contains(x.CreatedBy),
            cancellationToken);

        if (lesson is null)
        {
            return CreateParentAssignmentResult.BadRequest("Lesson does not belong to current parent.");
        }

        var assignment = new Assignment
        {
            ChildId = request.ChildId,
            LessonId = request.LessonId,
            AssignedAt = DateTime.UtcNow,
            DueDate = request.DueDate,
            Status = "Assigned"
        };

        _db.Assignments.Add(assignment);
        await _db.SaveChangesAsync(cancellationToken);

        var response = new AssignmentResponse(
            assignment.Id,
            assignment.ChildId,
            assignment.LessonId,
            lesson.Title,
            lesson.Subject,
            assignment.AssignedAt,
            assignment.DueDate,
            assignment.Status);

        return CreateParentAssignmentResult.Created(response);
    }
}

public sealed class CreateParentAssignmentCommandValidator : IRequestValidator<CreateParentAssignmentCommand>
{
    public IEnumerable<string> Validate(CreateParentAssignmentCommand request)
    {
        if (request.ParentId == Guid.Empty)
        {
            yield return "Child does not belong to current parent.";
        }
    }
}