using MediatR;

public sealed record GetParentAssignmentsQuery(Guid ParentId, Guid? ChildId) : IRequest<IReadOnlyList<AssignmentResponse>>;

public sealed class GetParentAssignmentsQueryHandler : IRequestHandler<GetParentAssignmentsQuery, IReadOnlyList<AssignmentResponse>>
{
    private readonly IAssignmentReadService _assignmentReadService;

    public GetParentAssignmentsQueryHandler(IAssignmentReadService assignmentReadService)
    {
        _assignmentReadService = assignmentReadService;
    }

    public async Task<IReadOnlyList<AssignmentResponse>> Handle(GetParentAssignmentsQuery query, CancellationToken cancellationToken)
    {
        return await _assignmentReadService.ListForParentAsync(query.ParentId, query.ChildId);
    }
}