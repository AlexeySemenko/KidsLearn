public class ParentAccountLink
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ParentAId { get; set; }
    public Guid ParentBId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public AppUser ParentA { get; set; } = null!;
    public AppUser ParentB { get; set; } = null!;
}
