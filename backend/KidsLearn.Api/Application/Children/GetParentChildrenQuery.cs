using MediatR;
using Microsoft.EntityFrameworkCore;

public sealed record GetParentChildrenQuery(Guid ParentId) : IRequest<IReadOnlyList<ChildResponse>>;

public sealed class GetParentChildrenQueryHandler : IRequestHandler<GetParentChildrenQuery, IReadOnlyList<ChildResponse>>
{
    private readonly AppDbContext _db;

    public GetParentChildrenQueryHandler(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ChildResponse>> Handle(GetParentChildrenQuery query, CancellationToken cancellationToken)
    {
        var scopedParentIds = await ApiEndpointHelpers.ResolveParentScopeIdsAsync(_db, query.ParentId);

        return await _db.Children
            .AsNoTracking()
            .Where(x => scopedParentIds.Contains(x.ParentId))
            .Select(x => new ChildResponse(x.Id, x.ParentId, x.Name, x.Grade, x.User != null ? x.User.Email : null))
            .ToListAsync(cancellationToken);
    }
}