using MediatR;

public sealed record GetChildResultDetailQuery(Guid ChildId, Guid ResultId)
    : IRequest<GetChildResultDetailResult>, IValidationFailureResponseFactory<GetChildResultDetailResult>
{
    public GetChildResultDetailResult CreateValidationFailureResponse(string error)
    {
        return GetChildResultDetailResult.BadRequest(error);
    }
}

public sealed record GetChildResultDetailResult(ResultDetailResponse? Response, string? Error, int StatusCode)
{
    public static GetChildResultDetailResult BadRequest(string error)
        => new(null, error, StatusCodes.Status400BadRequest);

    public static GetChildResultDetailResult FromServiceResult(ServiceResult<ResultDetailResponse> serviceResult)
        => new(serviceResult.Value, serviceResult.Error, serviceResult.StatusCode);
}

public sealed class GetChildResultDetailQueryHandler
    : IRequestHandler<GetChildResultDetailQuery, GetChildResultDetailResult>
{
    private readonly IAssignmentSolvingService _solvingService;

    public GetChildResultDetailQueryHandler(IAssignmentSolvingService solvingService)
    {
        _solvingService = solvingService;
    }

    public async Task<GetChildResultDetailResult> Handle(GetChildResultDetailQuery query, CancellationToken cancellationToken)
    {
        var result = await _solvingService.GetResultAsync(
            AssignmentAccessScope.Child,
            query.ChildId,
            query.ResultId);

        return GetChildResultDetailResult.FromServiceResult(result);
    }
}

public sealed class GetChildResultDetailQueryValidator : IRequestValidator<GetChildResultDetailQuery>
{
    public IEnumerable<string> Validate(GetChildResultDetailQuery request)
    {
        if (request.ChildId == Guid.Empty || request.ResultId == Guid.Empty)
        {
            yield return "Result not found.";
        }
    }
}