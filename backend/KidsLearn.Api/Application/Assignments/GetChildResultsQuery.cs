using MediatR;

public sealed record GetChildResultsQuery(Guid ChildId) : IRequest<List<ResultListItemResponse>>;

public sealed class GetChildResultsQueryHandler : IRequestHandler<GetChildResultsQuery, List<ResultListItemResponse>>
{
    private readonly IAssignmentSolvingService _solvingService;

    public GetChildResultsQueryHandler(IAssignmentSolvingService solvingService)
    {
        _solvingService = solvingService;
    }

    public Task<List<ResultListItemResponse>> Handle(GetChildResultsQuery query, CancellationToken cancellationToken)
        => _solvingService.GetResultsAsync(query.ChildId);
}
