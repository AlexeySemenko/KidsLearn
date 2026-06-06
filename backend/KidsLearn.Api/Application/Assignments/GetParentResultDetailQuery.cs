using MediatR;

public sealed record GetParentResultDetailQuery(Guid ParentId, Guid ResultId)
    : IRequest<GetParentResultDetailResult>, IValidationFailureResponseFactory<GetParentResultDetailResult>
{
    public GetParentResultDetailResult CreateValidationFailureResponse(string error)
    {
        return GetParentResultDetailResult.BadRequest(error);
    }
}

public sealed record GetParentResultDetailResult(ResultDetailResponse? Response, string? Error, int StatusCode)
{
    public static GetParentResultDetailResult BadRequest(string error)
        => new(null, error, StatusCodes.Status400BadRequest);

    public static GetParentResultDetailResult FromServiceResult(ServiceResult<ResultDetailResponse> serviceResult)
        => new(serviceResult.Value, serviceResult.Error, serviceResult.StatusCode);
}

public sealed class GetParentResultDetailQueryHandler
    : IRequestHandler<GetParentResultDetailQuery, GetParentResultDetailResult>
{
    private readonly IAssignmentSolvingService _solvingService;

    public GetParentResultDetailQueryHandler(IAssignmentSolvingService solvingService)
    {
        _solvingService = solvingService;
    }

    public async Task<GetParentResultDetailResult> Handle(GetParentResultDetailQuery query, CancellationToken cancellationToken)
    {
        var result = await _solvingService.GetResultAsync(
            AssignmentAccessScope.Parent,
            query.ParentId,
            query.ResultId);

        return GetParentResultDetailResult.FromServiceResult(result);
    }
}

public sealed class GetParentResultDetailQueryValidator : IRequestValidator<GetParentResultDetailQuery>
{
    public IEnumerable<string> Validate(GetParentResultDetailQuery request)
    {
        if (request.ParentId == Guid.Empty || request.ResultId == Guid.Empty)
        {
            yield return "Result not found.";
        }
    }
}