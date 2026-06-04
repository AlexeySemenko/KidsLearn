using MediatR;

public sealed record GetParentAssignmentForSolvingQuery(Guid ParentId, Guid AssignmentId)
    : IRequest<GetParentAssignmentForSolvingResult>, IValidationFailureResponseFactory<GetParentAssignmentForSolvingResult>
{
    public GetParentAssignmentForSolvingResult CreateValidationFailureResponse(string error)
    {
        return GetParentAssignmentForSolvingResult.BadRequest(error);
    }
}

public sealed record GetParentAssignmentForSolvingResult(AssignmentForSolvingResponse? Response, string? Error, int StatusCode)
{
    public static GetParentAssignmentForSolvingResult BadRequest(string error)
        => new(null, error, StatusCodes.Status400BadRequest);

    public static GetParentAssignmentForSolvingResult FromServiceResult(ServiceResult<AssignmentForSolvingResponse> serviceResult)
        => new(serviceResult.Value, serviceResult.Error, serviceResult.StatusCode);
}

public sealed class GetParentAssignmentForSolvingQueryHandler
    : IRequestHandler<GetParentAssignmentForSolvingQuery, GetParentAssignmentForSolvingResult>
{
    private readonly IAssignmentSolvingService _solvingService;

    public GetParentAssignmentForSolvingQueryHandler(IAssignmentSolvingService solvingService)
    {
        _solvingService = solvingService;
    }

    public async Task<GetParentAssignmentForSolvingResult> Handle(GetParentAssignmentForSolvingQuery query, CancellationToken cancellationToken)
    {
        var result = await _solvingService.GetForSolvingAsync(
            AssignmentAccessScope.Parent,
            query.ParentId,
            query.AssignmentId);

        return GetParentAssignmentForSolvingResult.FromServiceResult(result);
    }
}

public sealed class GetParentAssignmentForSolvingQueryValidator : IRequestValidator<GetParentAssignmentForSolvingQuery>
{
    public IEnumerable<string> Validate(GetParentAssignmentForSolvingQuery request)
    {
        if (request.ParentId == Guid.Empty || request.AssignmentId == Guid.Empty)
        {
            yield return "Assignment not found.";
        }
    }
}