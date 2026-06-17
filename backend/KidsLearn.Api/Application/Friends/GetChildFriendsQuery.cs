using MediatR;
using Microsoft.EntityFrameworkCore;

public sealed record GetChildFriendsQuery(Guid ChildId) : IRequest<IReadOnlyList<FriendResponse>>;

public sealed class GetChildFriendsQueryHandler : IRequestHandler<GetChildFriendsQuery, IReadOnlyList<FriendResponse>>
{
    private readonly AppDbContext _db;

    public GetChildFriendsQueryHandler(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<FriendResponse>> Handle(GetChildFriendsQuery query, CancellationToken cancellationToken)
    {
        var childId = query.ChildId;

        var asRequester = await _db.ChildFriendships
            .AsNoTracking()
            .Where(x => x.RequesterId == childId && x.Status == "Accepted" && x.AcceptorId != null)
            .Select(x => new FriendResponse(
                x.Id,
                x.Acceptor!.Id,
                x.Acceptor.Name,
                x.Acceptor.Grade,
                x.AcceptedAt!.Value))
            .ToListAsync(cancellationToken);

        var asAcceptor = await _db.ChildFriendships
            .AsNoTracking()
            .Where(x => x.AcceptorId == childId && x.Status == "Accepted")
            .Select(x => new FriendResponse(
                x.Id,
                x.Requester.Id,
                x.Requester.Name,
                x.Requester.Grade,
                x.AcceptedAt!.Value))
            .ToListAsync(cancellationToken);

        return asRequester.Concat(asAcceptor)
            .OrderBy(x => x.Name)
            .ToList();
    }
}
