using MediatR;
using Microsoft.EntityFrameworkCore;

public sealed record GetFriendNoteQuery(Guid RequestingChildId, Guid FriendChildId) : IRequest<GetFriendNoteResult>;
public sealed record GetFriendNoteResult(int StatusCode, string? MyNote, string? Error = null);

public sealed class GetFriendNoteQueryHandler(AppDbContext db)
    : IRequestHandler<GetFriendNoteQuery, GetFriendNoteResult>
{
    public async Task<GetFriendNoteResult> Handle(GetFriendNoteQuery query, CancellationToken cancellationToken)
    {
        var friendship = await db.ChildFriendships
            .Where(f => f.Status == "Accepted" &&
                ((f.RequesterId == query.RequestingChildId && f.AcceptorId == query.FriendChildId) ||
                 (f.RequesterId == query.FriendChildId && f.AcceptorId == query.RequestingChildId)))
            .FirstOrDefaultAsync(cancellationToken);

        if (friendship == null)
            return new GetFriendNoteResult(StatusCodes.Status403Forbidden, null, "Not friends.");

        var now = DateTime.UtcNow;

        if (friendship.RequesterId == query.RequestingChildId)
        {
            // I am the requester — my note is NoteFromRequester; friend's note to read = NoteFromAcceptor
            if (friendship.NoteFromAcceptor != null)
                friendship.NoteFromAcceptorReadAt = now;
            await db.SaveChangesAsync(cancellationToken);
            return new GetFriendNoteResult(StatusCodes.Status200OK, friendship.NoteFromRequester);
        }
        else
        {
            // I am the acceptor — my note is NoteFromAcceptor; friend's note to read = NoteFromRequester
            if (friendship.NoteFromRequester != null)
                friendship.NoteFromRequesterReadAt = now;
            await db.SaveChangesAsync(cancellationToken);
            return new GetFriendNoteResult(StatusCodes.Status200OK, friendship.NoteFromAcceptor);
        }
    }
}
