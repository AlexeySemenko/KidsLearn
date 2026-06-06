using MediatR;

public sealed record GetChildAssignmentsQuery(Guid ChildId) : IRequest<IReadOnlyList<AssignmentResponse>>;

public sealed class GetChildAssignmentsQueryHandler : IRequestHandler<GetChildAssignmentsQuery, IReadOnlyList<AssignmentResponse>>
{
    private readonly IAssignmentReadService _assignmentReadService;

    public GetChildAssignmentsQueryHandler(IAssignmentReadService assignmentReadService)
    {
        _assignmentReadService = assignmentReadService;
    }

    public async Task<IReadOnlyList<AssignmentResponse>> Handle(GetChildAssignmentsQuery query, CancellationToken cancellationToken)
    {
        return await _assignmentReadService.ListForChildAsync(query.ChildId);
    }
}