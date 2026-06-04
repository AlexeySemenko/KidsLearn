using MediatR;

public sealed record SubmitChildAssignmentAnswersCommand(Guid ChildId, Guid AssignmentId, SubmitAssignmentAnswersRequest Request)
    : IRequest<SubmitChildAssignmentAnswersResult>, IValidationFailureResponseFactory<SubmitChildAssignmentAnswersResult>
{
    public SubmitChildAssignmentAnswersResult CreateValidationFailureResponse(string error)
    {
        return SubmitChildAssignmentAnswersResult.BadRequest(error);
    }
}

public sealed record SubmitChildAssignmentAnswersResult(SubmitAssignmentAnswersResponse? Response, string? Error, int StatusCode)
{
    public static SubmitChildAssignmentAnswersResult BadRequest(string error)
        => new(null, error, StatusCodes.Status400BadRequest);

    public static SubmitChildAssignmentAnswersResult FromServiceResult(ServiceResult<SubmitAssignmentAnswersResponse> serviceResult)
        => new(serviceResult.Value, serviceResult.Error, serviceResult.StatusCode);
}

public sealed class SubmitChildAssignmentAnswersCommandHandler
    : IRequestHandler<SubmitChildAssignmentAnswersCommand, SubmitChildAssignmentAnswersResult>
{
    private readonly IAssignmentSolvingService _solvingService;

    public SubmitChildAssignmentAnswersCommandHandler(IAssignmentSolvingService solvingService)
    {
        _solvingService = solvingService;
    }

    public async Task<SubmitChildAssignmentAnswersResult> Handle(SubmitChildAssignmentAnswersCommand command, CancellationToken cancellationToken)
    {
        var result = await _solvingService.SubmitAnswersAsync(
            AssignmentAccessScope.Child,
            command.ChildId,
            command.AssignmentId,
            command.Request);

        return SubmitChildAssignmentAnswersResult.FromServiceResult(result);
    }
}

public sealed class SubmitChildAssignmentAnswersCommandValidator : IRequestValidator<SubmitChildAssignmentAnswersCommand>
{
    public IEnumerable<string> Validate(SubmitChildAssignmentAnswersCommand request)
    {
        if (request.ChildId == Guid.Empty || request.AssignmentId == Guid.Empty)
        {
            yield return "At least one answer is required.";
            yield break;
        }

        if (request.Request.Answers is null || request.Request.Answers.Count == 0)
        {
            yield return "At least one answer is required.";
        }
    }
}