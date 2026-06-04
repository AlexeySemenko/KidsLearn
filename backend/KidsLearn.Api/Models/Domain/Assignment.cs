public class Assignment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ChildId { get; set; }
    public Guid LessonId { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DueDate { get; set; }
    public string Status { get; set; } = "Assigned";

    public Child Child { get; set; } = null!;
    public Lesson Lesson { get; set; } = null!;
    public AssignmentResult? Result { get; set; }
    public ICollection<AssignmentAnswer> Answers { get; set; } = new List<AssignmentAnswer>();
}