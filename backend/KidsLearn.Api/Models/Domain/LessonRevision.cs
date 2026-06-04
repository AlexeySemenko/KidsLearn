public class LessonRevision
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LessonId { get; set; }
    public int RevisionNumber { get; set; }
    public string SnapshotJson { get; set; } = string.Empty;
    public string DiffSummary { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Lesson Lesson { get; set; } = null!;
}
