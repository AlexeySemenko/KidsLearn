public class Child
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ParentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Grade { get; set; }

    public AppUser Parent { get; set; } = null!;
    public ICollection<Assignment> Assignments { get; set; } = new List<Assignment>();
}