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

        // Child is the requester — friend is the acceptor; unread = acceptor's note not yet read by requester
        var asRequester = await _db.ChildFriendships
            .AsNoTracking()
            .Where(x => x.RequesterId == childId && x.Status == "Accepted" && x.AcceptorId != null)
            .Select(x => new FriendResponse(
                x.Id,
                x.Acceptor!.Id,
                x.Acceptor.Name,
                x.Acceptor.Grade,
                x.AcceptedAt!.Value,
                x.NoteFromAcceptor != null && (x.NoteFromAcceptorReadAt == null || x.NoteFromAcceptorAt > x.NoteFromAcceptorReadAt)))
            .ToListAsync(cancellationToken);

        // Child is the acceptor — friend is the requester; unread = requester's note not yet read by acceptor
        var asAcceptor = await _db.ChildFriendships
            .AsNoTracking()
            .Where(x => x.AcceptorId == childId && x.Status == "Accepted")
            .Select(x => new FriendResponse(
                x.Id,
                x.Requester.Id,
                x.Requester.Name,
                x.Requester.Grade,
                x.AcceptedAt!.Value,
                x.NoteFromRequester != null && (x.NoteFromRequesterReadAt == null || x.NoteFromRequesterAt > x.NoteFromRequesterReadAt)))
            .ToListAsync(cancellationToken);

        return asRequester.Concat(asAcceptor)
            .OrderBy(x => x.Name)
            .ToList();
    }
}
