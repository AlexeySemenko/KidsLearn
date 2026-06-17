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

    public Child Requester { get; set; } = null!;
    public Child? Acceptor { get; set; }
}
