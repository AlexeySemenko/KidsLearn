using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

public sealed record UpdateFriendNoteCommand(Guid RequestingChildId, Guid FriendChildId, string? Note) : IRequest<int>;

public sealed class UpdateFriendNoteCommandHandler(AppDbContext db, IHubContext<FriendNotificationsHub> hub)
    : IRequestHandler<UpdateFriendNoteCommand, int>
{
    public async Task<int> Handle(UpdateFriendNoteCommand command, CancellationToken cancellationToken)
    {
        var friendship = await db.ChildFriendships
            .Where(f => f.Status == "Accepted" &&
                ((f.RequesterId == command.RequestingChildId && f.AcceptorId == command.FriendChildId) ||
                 (f.RequesterId == command.FriendChildId && f.AcceptorId == command.RequestingChildId)))
            .FirstOrDefaultAsync(cancellationToken);

        if (friendship == null)
            return StatusCodes.Status403Forbidden;

        var now = DateTime.UtcNow;
        var note = string.IsNullOrWhiteSpace(command.Note) ? null : command.Note.Trim();
        bool iAmRequester = friendship.RequesterId == command.RequestingChildId;

        if (iAmRequester)
        {
            friendship.NoteFromRequester = note;
            friendship.NoteFromRequesterAt = now;
            friendship.NoteFromRequesterReadAt = null; // recipient hasn't read yet
        }
        else
        {
            friendship.NoteFromAcceptor = note;
            friendship.NoteFromAcceptorAt = now;
            friendship.NoteFromAcceptorReadAt = null;
        }

        await db.SaveChangesAsync(cancellationToken);

        // Push live update to the friend
        var friendGroupId = (iAmRequester ? friendship.AcceptorId : (Guid?)friendship.RequesterId)
            ?.ToString("N");
        if (friendGroupId != null)
        {
            await hub.Clients.Group(friendGroupId).SendAsync(
                "FriendNoteUpdated",
                new { friendshipId = friendship.Id, lastNoteText = note, lastNoteIsFromMe = false },
                cancellationToken);
        }

        return StatusCodes.Status204NoContent;
    }
}
