using MediatR;

public sealed record GetParentChildResultsQuery(Guid ParentId, Guid ChildId) : IRequest<List<ResultListItemResponse>>;

public sealed class GetParentChildResultsQueryHandler(IAssignmentSolvingService solvingService)
    : IRequestHandler<GetParentChildResultsQuery, List<ResultListItemResponse>>
{
    public Task<List<ResultListItemResponse>> Handle(GetParentChildResultsQuery query, CancellationToken cancellationToken)
        => solvingService.GetChildResultsForParentAsync(query.ParentId, query.ChildId);
}
