public sealed record FriendResponse(
    Guid FriendshipId,
    Guid ChildId,
    string Name,
    int Grade,
    DateTime FriendsSince,
    bool HasUnreadMessage);

public sealed record FriendInviteInfoResponse(
    Guid FriendshipId,
    string RequesterName,
    int RequesterGrade,
    string Status);

public sealed record FriendNoteResponse(string? LastNoteText, bool LastNoteIsFromMe, string? MyNote, string? TheirNote);

public sealed record SendFriendInviteRequest(string Email);
public sealed record UpdateFriendNoteRequest(string? Note);
