public class AssignmentResult
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AssignmentId { get; set; }
    public decimal Score { get; set; }
    public int CorrectAnswers { get; set; }
    public int TotalQuestions { get; set; }
    public DateTime CompletedAt { get; set; }

    public Assignment Assignment { get; set; } = null!;
}