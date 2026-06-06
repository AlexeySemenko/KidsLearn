public class Question
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LessonId { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
    public int Order { get; set; }

    public Lesson Lesson { get; set; } = null!;
    public ICollection<AnswerOption> Answers { get; set; } = new List<AnswerOption>();
    public ICollection<AssignmentAnswer> AssignmentAnswers { get; set; } = new List<AssignmentAnswer>();
}