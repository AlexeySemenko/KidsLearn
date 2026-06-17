using MediatR;
using Microsoft.EntityFrameworkCore;

public sealed record AcceptChildFriendInviteCommand(Guid ChildId, string Token) : IRequest<AcceptChildFriendInviteResult>;

public sealed record AcceptChildFriendInviteResult(FriendResponse? Friend, string? Error, int StatusCode)
{
    public static AcceptChildFriendInviteResult Ok(FriendResponse friend) => new(friend, null, StatusCodes.Status200OK);
    public static AcceptChildFriendInviteResult BadRequest(string error) => new(null, error, StatusCodes.Status400BadRequest);
    public static AcceptChildFriendInviteResult NotFound(string error) => new(null, error, StatusCodes.Status404NotFound);
    public static AcceptChildFriendInviteResult Conflict(string error) => new(null, error, StatusCodes.Status409Conflict);
}

public sealed class AcceptChildFriendInviteCommandHandler : IRequestHandler<AcceptChildFriendInviteCommand, AcceptChildFriendInviteResult>
{
    private readonly AppDbContext _db;

    public AcceptChildFriendInviteCommandHandler(AppDbContext db)
    {
        _db = db;
    }

    public async Task<AcceptChildFriendInviteResult> Handle(AcceptChildFriendInviteCommand command, CancellationToken cancellationToken)
    {
        var friendship = await _db.ChildFriendships
            .Include(x => x.Requester)
            .FirstOrDefaultAsync(x => x.InviteToken == command.Token, cancellationToken);

        if (friendship is null)
        {
            return AcceptChildFriendInviteResult.NotFound("Invitation not found.");
        }

        if (friendship.Status != "Pending")
        {
            return AcceptChildFriendInviteResult.Conflict("This invitation has already been accepted.");
        }

        if (friendship.RequesterId == command.ChildId)
        {
            return AcceptChildFriendInviteResult.BadRequest("You cannot accept your own invitation.");
        }

        friendship.AcceptorId = command.ChildId;
        friendship.Status = "Accepted";
        friendship.AcceptedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        var acceptor = await _db.Children
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == command.ChildId, cancellationToken);

        return AcceptChildFriendInviteResult.Ok(new FriendResponse(
            friendship.Id,
            friendship.Requester.Id,
            friendship.Requester.Name,
            friendship.Requester.Grade,
            friendship.AcceptedAt!.Value));
    }
}
