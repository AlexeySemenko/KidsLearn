public sealed record FriendResponse(
    Guid FriendshipId,
    Guid ChildId,
    string Name,
    int Grade,
    DateTime FriendsSince);

public sealed record FriendInviteInfoResponse(
    Guid FriendshipId,
    string RequesterName,
    int RequesterGrade,
    string Status);

public sealed record SendFriendInviteRequest(string Email);
