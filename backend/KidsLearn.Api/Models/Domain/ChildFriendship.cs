public class ChildFriendship
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RequesterId { get; set; }
    public Guid? AcceptorId { get; set; }
    public string InviteeEmail { get; set; } = string.Empty;
    public string InviteToken { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? AcceptedAt { get; set; }

    // Notes: each side can leave one message for the other
    public string? NoteFromRequester { get; set; }
    public DateTime? NoteFromRequesterAt { get; set; }
    public DateTime? NoteFromRequesterReadAt { get; set; }  // last read by Acceptor

    public string? NoteFromAcceptor { get; set; }
    public DateTime? NoteFromAcceptorAt { get; set; }
    public DateTime? NoteFromAcceptorReadAt { get; set; }   // last read by Requester

    public Child Requester { get; set; } = null!;
    public Child? Acceptor { get; set; }
}
