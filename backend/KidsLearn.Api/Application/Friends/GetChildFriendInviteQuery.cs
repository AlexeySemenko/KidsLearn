using MediatR;
using Microsoft.EntityFrameworkCore;

public sealed record GetChildFriendInviteQuery(string Token) : IRequest<GetChildFriendInviteResult>;

public sealed record GetChildFriendInviteResult(FriendInviteInfoResponse? Response, string? Error, int StatusCode)
{
    public static GetChildFriendInviteResult Ok(FriendInviteInfoResponse response) => new(response, null, StatusCodes.Status200OK);
    public static GetChildFriendInviteResult NotFound(string error) => new(null, error, StatusCodes.Status404NotFound);
}

public sealed class GetChildFriendInviteQueryHandler : IRequestHandler<GetChildFriendInviteQuery, GetChildFriendInviteResult>
{
    private readonly AppDbContext _db;

    public GetChildFriendInviteQueryHandler(AppDbContext db)
    {
        _db = db;
    }

    public async Task<GetChildFriendInviteResult> Handle(GetChildFriendInviteQuery query, CancellationToken cancellationToken)
    {
        var invite = await _db.ChildFriendships
            .AsNoTracking()
            .Where(x => x.InviteToken == query.Token)
            .Select(x => new
            {
                x.Id,
                x.Status,
                x.Requester.Name,
                x.Requester.Grade
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (invite is null)
        {
            return GetChildFriendInviteResult.NotFound("Invitation not found.");
        }

        return GetChildFriendInviteResult.Ok(new FriendInviteInfoResponse(
            invite.Id,
            invite.Name,
            invite.Grade,
            invite.Status));
    }
}
