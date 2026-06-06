using MediatR;

public sealed record GetChildAssignmentForSolvingQuery(Guid ChildId, Guid AssignmentId)
    : IRequest<GetChildAssignmentForSolvingResult>, IValidationFailureResponseFactory<GetChildAssignmentForSolvingResult>
{
    public GetChildAssignmentForSolvingResult CreateValidationFailureResponse(string error)
    {
        return GetChildAssignmentForSolvingResult.BadRequest(error);
    }
}

public sealed record GetChildAssignmentForSolvingResult(AssignmentForSolvingResponse? Response, string? Error, int StatusCode)
{
    public static GetChildAssignmentForSolvingResult BadRequest(string error)
        => new(null, error, StatusCodes.Status400BadRequest);

    public static GetChildAssignmentForSolvingResult FromServiceResult(ServiceResult<AssignmentForSolvingResponse> serviceResult)
        => new(serviceResult.Value, serviceResult.Error, serviceResult.StatusCode);
}

public sealed class GetChildAssignmentForSolvingQueryHandler
    : IRequestHandler<GetChildAssignmentForSolvingQuery, GetChildAssignmentForSolvingResult>
{
    private readonly IAssignmentSolvingService _solvingService;

    public GetChildAssignmentForSolvingQueryHandler(IAssignmentSolvingService solvingService)
    {
        _solvingService = solvingService;
    }

    public async Task<GetChildAssignmentForSolvingResult> Handle(GetChildAssignmentForSolvingQuery query, CancellationToken cancellationToken)
    {
        var result = await _solvingService.GetForSolvingAsync(
            AssignmentAccessScope.Child,
            query.ChildId,
            query.AssignmentId);

        return GetChildAssignmentForSolvingResult.FromServiceResult(result);
    }
}

public sealed class GetChildAssignmentForSolvingQueryValidator : IRequestValidator<GetChildAssignmentForSolvingQuery>
{
    public IEnumerable<string> Validate(GetChildAssignmentForSolvingQuery request)
    {
        if (request.ChildId == Guid.Empty || request.AssignmentId == Guid.Empty)
        {
            yield return "Assignment not found.";
        }
    }
}