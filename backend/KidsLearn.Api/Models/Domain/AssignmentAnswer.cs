public class AssignmentAnswer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AssignmentId { get; set; }
    public Guid QuestionId { get; set; }
    public Guid? SelectedAnswerOptionId { get; set; }
    public string? TextAnswer { get; set; }
    public bool IsCorrect { get; set; }
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

    public Assignment Assignment { get; set; } = null!;
    public Question Question { get; set; } = null!;
    public AnswerOption? SelectedAnswerOption { get; set; }
}