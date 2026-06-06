using MediatR;

public sealed record SubmitParentAssignmentAnswersCommand(Guid ParentId, Guid AssignmentId, SubmitAssignmentAnswersRequest Request)
    : IRequest<SubmitParentAssignmentAnswersResult>, IValidationFailureResponseFactory<SubmitParentAssignmentAnswersResult>
{
    public SubmitParentAssignmentAnswersResult CreateValidationFailureResponse(string error)
    {
        return SubmitParentAssignmentAnswersResult.BadRequest(error);
    }
}

public sealed record SubmitParentAssignmentAnswersResult(SubmitAssignmentAnswersResponse? Response, string? Error, int StatusCode)
{
    public static SubmitParentAssignmentAnswersResult BadRequest(string error)
        => new(null, error, StatusCodes.Status400BadRequest);

    public static SubmitParentAssignmentAnswersResult FromServiceResult(ServiceResult<SubmitAssignmentAnswersResponse> serviceResult)
        => new(serviceResult.Value, serviceResult.Error, serviceResult.StatusCode);
}

public sealed class SubmitParentAssignmentAnswersCommandHandler
    : IRequestHandler<SubmitParentAssignmentAnswersCommand, SubmitParentAssignmentAnswersResult>
{
    private readonly IAssignmentSolvingService _solvingService;

    public SubmitParentAssignmentAnswersCommandHandler(IAssignmentSolvingService solvingService)
    {
        _solvingService = solvingService;
    }

    public async Task<SubmitParentAssignmentAnswersResult> Handle(SubmitParentAssignmentAnswersCommand command, CancellationToken cancellationToken)
    {
        var result = await _solvingService.SubmitAnswersAsync(
            AssignmentAccessScope.Parent,
            command.ParentId,
            command.AssignmentId,
            command.Request);

        return SubmitParentAssignmentAnswersResult.FromServiceResult(result);
    }
}

public sealed class SubmitParentAssignmentAnswersCommandValidator : IRequestValidator<SubmitParentAssignmentAnswersCommand>
{
    public IEnumerable<string> Validate(SubmitParentAssignmentAnswersCommand request)
    {
        if (request.ParentId == Guid.Empty || request.AssignmentId == Guid.Empty)
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