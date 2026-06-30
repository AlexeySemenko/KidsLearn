public class Child
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ParentId { get; set; }
    public Guid? UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Grade { get; set; }
    public string? EnrollmentEmail { get; set; }
    public string? RegistrationToken { get; set; }

    public AppUser Parent { get; set; } = null!;
    public AppUser? User { get; set; }
    public ICollection<Assignment> Assignments { get; set; } = new List<Assignment>();
}