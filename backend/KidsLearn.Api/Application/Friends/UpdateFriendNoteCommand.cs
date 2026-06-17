using MediatR;
using Microsoft.EntityFrameworkCore;

public sealed record UpdateFriendNoteCommand(Guid RequestingChildId, Guid FriendChildId, string? Note) : IRequest<int>;

public sealed class UpdateFriendNoteCommandHandler(AppDbContext db)
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

        if (friendship.RequesterId == command.RequestingChildId)
        {
            friendship.NoteFromRequester = note;
            friendship.NoteFromRequesterAt = now;
            // Reset the read timestamp so the friend sees the envelope
            friendship.NoteFromRequesterReadAt = null;
        }
        else
        {
            friendship.NoteFromAcceptor = note;
            friendship.NoteFromAcceptorAt = now;
            friendship.NoteFromAcceptorReadAt = null;
        }

        await db.SaveChangesAsync(cancellationToken);
        return StatusCodes.Status204NoContent;
    }
}
