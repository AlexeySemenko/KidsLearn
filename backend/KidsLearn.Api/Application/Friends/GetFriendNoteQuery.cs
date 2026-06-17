using MediatR;
using Microsoft.EntityFrameworkCore;

public sealed record GetFriendNoteQuery(Guid RequestingChildId, Guid FriendChildId) : IRequest<GetFriendNoteResult>;
public sealed record GetFriendNoteResult(int StatusCode, string? LastNoteText, bool LastNoteIsFromMe, string? MyNote, string? TheirNote, string? Error = null);

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
            return new GetFriendNoteResult(StatusCodes.Status403Forbidden, null, false, null, null, "Not friends.");

        bool iAmRequester = friendship.RequesterId == query.RequestingChildId;
        var myNote   = iAmRequester ? friendship.NoteFromRequester : friendship.NoteFromAcceptor;
        var theirNote = iAmRequester ? friendship.NoteFromAcceptor  : friendship.NoteFromRequester;
        var myNoteAt  = iAmRequester ? friendship.NoteFromRequesterAt : friendship.NoteFromAcceptorAt;
        var theirNoteAt = iAmRequester ? friendship.NoteFromAcceptorAt : friendship.NoteFromRequesterAt;

        // Determine which message is the most recent
        bool lastIsFromMe;
        string? lastNoteText;

        if (myNoteAt == null && theirNoteAt == null)
        {
            lastNoteText = null;
            lastIsFromMe = false;
        }
        else if (myNoteAt >= theirNoteAt)
        {
            lastNoteText = myNote;
            lastIsFromMe = true;
        }
        else
        {
            lastNoteText = theirNote;
            lastIsFromMe = false;
        }

        // Mark their note as read
        var now = DateTime.UtcNow;
        if (iAmRequester && friendship.NoteFromAcceptor != null)
            friendship.NoteFromAcceptorReadAt = now;
        else if (!iAmRequester && friendship.NoteFromRequester != null)
            friendship.NoteFromRequesterReadAt = now;

        await db.SaveChangesAsync(cancellationToken);
        return new GetFriendNoteResult(StatusCodes.Status200OK, lastNoteText, lastIsFromMe, myNote, theirNote);
    }
}
