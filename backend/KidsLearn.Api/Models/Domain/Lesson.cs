public class Lesson
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public int Grade { get; set; }
    public string Topic { get; set; } = string.Empty;
    public string Difficulty { get; set; } = "Medium";
    public string? Story { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Question> Questions { get; set; } = new List<Question>();
    public ICollection<Assignment> Assignments { get; set; } = new List<Assignment>();
    public ICollection<LessonRevision> Revisions { get; set; } = new List<LessonRevision>();
}