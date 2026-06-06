using MediatR;

public sealed record CompleteChildAssignmentCommand(Guid ChildId, Guid AssignmentId)
    : IRequest<CompleteChildAssignmentResult>, IValidationFailureResponseFactory<CompleteChildAssignmentResult>
{
    public CompleteChildAssignmentResult CreateValidationFailureResponse(string error)
    {
        return CompleteChildAssignmentResult.BadRequest(error);
    }
}

public sealed record CompleteChildAssignmentResult(CompleteAssignmentResponse? Response, string? Error, int StatusCode)
{
    public static CompleteChildAssignmentResult BadRequest(string error)
        => new(null, error, StatusCodes.Status400BadRequest);

    public static CompleteChildAssignmentResult FromServiceResult(ServiceResult<CompleteAssignmentResponse> serviceResult)
        => new(serviceResult.Value, serviceResult.Error, serviceResult.StatusCode);
}

public sealed class CompleteChildAssignmentCommandHandler
    : IRequestHandler<CompleteChildAssignmentCommand, CompleteChildAssignmentResult>
{
    private readonly IAssignmentSolvingService _solvingService;

    public CompleteChildAssignmentCommandHandler(IAssignmentSolvingService solvingService)
    {
        _solvingService = solvingService;
    }

    public async Task<CompleteChildAssignmentResult> Handle(CompleteChildAssignmentCommand command, CancellationToken cancellationToken)
    {
        var result = await _solvingService.CompleteAsync(
            AssignmentAccessScope.Child,
            command.ChildId,
            command.AssignmentId);

        return CompleteChildAssignmentResult.FromServiceResult(result);
    }
}

public sealed class CompleteChildAssignmentCommandValidator : IRequestValidator<CompleteChildAssignmentCommand>
{
    public IEnumerable<string> Validate(CompleteChildAssignmentCommand request)
    {
        if (request.ChildId == Guid.Empty || request.AssignmentId == Guid.Empty)
        {
            yield return "Assignment not found.";
        }
    }
}