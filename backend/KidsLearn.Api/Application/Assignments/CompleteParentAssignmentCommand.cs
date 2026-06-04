using MediatR;

public sealed record CompleteParentAssignmentCommand(Guid ParentId, Guid AssignmentId)
    : IRequest<CompleteParentAssignmentResult>, IValidationFailureResponseFactory<CompleteParentAssignmentResult>
{
    public CompleteParentAssignmentResult CreateValidationFailureResponse(string error)
    {
        return CompleteParentAssignmentResult.BadRequest(error);
    }
}

public sealed record CompleteParentAssignmentResult(CompleteAssignmentResponse? Response, string? Error, int StatusCode)
{
    public static CompleteParentAssignmentResult BadRequest(string error)
        => new(null, error, StatusCodes.Status400BadRequest);

    public static CompleteParentAssignmentResult FromServiceResult(ServiceResult<CompleteAssignmentResponse> serviceResult)
        => new(serviceResult.Value, serviceResult.Error, serviceResult.StatusCode);
}

public sealed class CompleteParentAssignmentCommandHandler
    : IRequestHandler<CompleteParentAssignmentCommand, CompleteParentAssignmentResult>
{
    private readonly IAssignmentSolvingService _solvingService;

    public CompleteParentAssignmentCommandHandler(IAssignmentSolvingService solvingService)
    {
        _solvingService = solvingService;
    }

    public async Task<CompleteParentAssignmentResult> Handle(CompleteParentAssignmentCommand command, CancellationToken cancellationToken)
    {
        var result = await _solvingService.CompleteAsync(
            AssignmentAccessScope.Parent,
            command.ParentId,
            command.AssignmentId);

        return CompleteParentAssignmentResult.FromServiceResult(result);
    }
}

public sealed class CompleteParentAssignmentCommandValidator : IRequestValidator<CompleteParentAssignmentCommand>
{
    public IEnumerable<string> Validate(CompleteParentAssignmentCommand request)
    {
        if (request.ParentId == Guid.Empty || request.AssignmentId == Guid.Empty)
        {
            yield return "Assignment not found.";
        }
    }
}