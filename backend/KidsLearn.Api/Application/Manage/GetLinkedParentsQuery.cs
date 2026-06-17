using MediatR;
using Microsoft.EntityFrameworkCore;

public sealed record GetLinkedParentsQuery(Guid ParentId) : IRequest<IReadOnlyList<LinkedParentResponse>>;

public sealed class GetLinkedParentsQueryHandler : IRequestHandler<GetLinkedParentsQuery, IReadOnlyList<LinkedParentResponse>>
{
    private readonly AppDbContext _db;

    public GetLinkedParentsQueryHandler(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<LinkedParentResponse>> Handle(GetLinkedParentsQuery query, CancellationToken cancellationToken)
    {
        var links = await _db.ParentAccountLinks
            .AsNoTracking()
            .Where(x => x.ParentAId == query.ParentId || x.ParentBId == query.ParentId)
            .Select(x => new
            {
                LinkedParentId = x.ParentAId == query.ParentId ? x.ParentBId : x.ParentAId,
                x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var linkedParentIds = links.Select(x => x.LinkedParentId).Distinct().ToList();

        var usersById = await _db.Users
            .AsNoTracking()
            .Where(x => linkedParentIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Email, cancellationToken);

        return links
            .Where(x => usersById.ContainsKey(x.LinkedParentId))
            .OrderBy(x => usersById[x.LinkedParentId])
            .Select(x => new LinkedParentResponse(x.LinkedParentId, usersById[x.LinkedParentId], x.CreatedAt))
            .ToList();
    }
}
