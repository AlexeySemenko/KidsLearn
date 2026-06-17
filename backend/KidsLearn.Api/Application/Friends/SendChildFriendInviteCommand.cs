using MediatR;
using Microsoft.EntityFrameworkCore;

public sealed record SendChildFriendInviteCommand(Guid ChildId, string Email, string AppBaseUrl)
    : IRequest<SendChildFriendInviteResult>;

public sealed record SendChildFriendInviteResult(string? Error, int StatusCode)
{
    public static SendChildFriendInviteResult Ok() => new(null, StatusCodes.Status200OK);
    public static SendChildFriendInviteResult BadRequest(string error) => new(error, StatusCodes.Status400BadRequest);
    public static SendChildFriendInviteResult NotFound(string error) => new(error, StatusCodes.Status404NotFound);
    public static SendChildFriendInviteResult Conflict(string error) => new(error, StatusCodes.Status409Conflict);
}

public sealed class SendChildFriendInviteCommandHandler : IRequestHandler<SendChildFriendInviteCommand, SendChildFriendInviteResult>
{
    private readonly AppDbContext _db;
    private readonly IEmailService _email;

    public SendChildFriendInviteCommandHandler(AppDbContext db, IEmailService email)
    {
        _db = db;
        _email = email;
    }

    public async Task<SendChildFriendInviteResult> Handle(SendChildFriendInviteCommand command, CancellationToken cancellationToken)
    {
        var normalizedEmail = command.Email.Trim().ToLowerInvariant();

        var requester = await _db.Children
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == command.ChildId, cancellationToken);

        if (requester is null)
        {
            return SendChildFriendInviteResult.NotFound("Child not found.");
        }

        // Prevent self-invite
        var requesterEmail = await _db.Users
            .AsNoTracking()
            .Where(x => x.Id == requester.UserId)
            .Select(x => x.Email)
            .FirstOrDefaultAsync(cancellationToken);

        if (requesterEmail != null && requesterEmail.ToLowerInvariant() == normalizedEmail)
        {
            return SendChildFriendInviteResult.BadRequest("You cannot invite yourself.");
        }

        // Check if already friends or invite pending
        var targetChild = await _db.Children
            .AsNoTracking()
            .Where(x => x.User != null && x.User.Email.ToLower() == normalizedEmail)
            .FirstOrDefaultAsync(cancellationToken);

        if (targetChild != null)
        {
            var alreadyLinked = await _db.ChildFriendships.AnyAsync(x =>
                (x.RequesterId == command.ChildId && x.AcceptorId == targetChild.Id) ||
                (x.AcceptorId == command.ChildId && x.RequesterId == targetChild.Id),
                cancellationToken);

            if (alreadyLinked)
            {
                return SendChildFriendInviteResult.Conflict("You are already friends or have a pending invite with this user.");
            }
        }

        // Check for an existing pending invite to the same email from this child
        var existingPending = await _db.ChildFriendships.AnyAsync(x =>
            x.RequesterId == command.ChildId &&
            x.InviteeEmail.ToLower() == normalizedEmail &&
            x.Status == "Pending",
            cancellationToken);

        if (existingPending)
        {
            return SendChildFriendInviteResult.Conflict("An invite to this email is already pending.");
        }

        var token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");

        var friendship = new ChildFriendship
        {
            RequesterId = command.ChildId,
            InviteeEmail = normalizedEmail,
            InviteToken = token,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };

        _db.ChildFriendships.Add(friendship);
        await _db.SaveChangesAsync(cancellationToken);

        var inviteUrl = $"{command.AppBaseUrl.TrimEnd('/')}/child/friends/invite/{token}";
        await _email.SendFriendInviteAsync(normalizedEmail, requester.Name, inviteUrl);

        return SendChildFriendInviteResult.Ok();
    }
}
