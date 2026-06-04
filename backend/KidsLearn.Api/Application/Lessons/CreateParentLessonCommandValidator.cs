public sealed class CreateParentLessonCommandValidator : IRequestValidator<CreateParentLessonCommand>
{
    public IEnumerable<string> Validate(CreateParentLessonCommand command)
    {
        var request = command.Request;

        if (string.IsNullOrWhiteSpace(request.Title)
            || string.IsNullOrWhiteSpace(request.Subject)
            || string.IsNullOrWhiteSpace(request.Topic)
            || !ApiEndpointHelpers.IsGradeInRange(request.Grade))
        {
            yield return "Title, subject, topic and grade (1-12) are required.";
            yield break;
        }

        if (request.Questions is null || request.Questions.Count == 0)
        {
            yield return "At least one question is required.";
            yield break;
        }

        foreach (var question in request.Questions)
        {
            if (string.IsNullOrWhiteSpace(question.QuestionText)
                || question.Answers is null
                || question.Answers.Count < 2)
            {
                yield return "Each question must have text and at least two answers.";
                yield break;
            }

            if (!question.Answers.Any(x => x.IsCorrect))
            {
                yield return "Each question must include at least one correct answer.";
                yield break;
            }

            if (question.Answers.Any(x => string.IsNullOrWhiteSpace(x.AnswerText)))
            {
                yield return "Answer text is required.";
                yield break;
            }
        }
    }
}