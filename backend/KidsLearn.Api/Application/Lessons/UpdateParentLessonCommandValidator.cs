public sealed class UpdateParentLessonCommandValidator : IRequestValidator<UpdateParentLessonCommand>
{
    public IEnumerable<string> Validate(UpdateParentLessonCommand request)
    {
        if (request.ParentId == Guid.Empty)
        {
            yield return "Parent id is required.";
            yield break;
        }

        if (request.LessonId == Guid.Empty)
        {
            yield return "Lesson id is required.";
            yield break;
        }

        var payload = request.Request;

        if (payload.Title is not null && string.IsNullOrWhiteSpace(payload.Title))
        {
            yield return "Title cannot be empty.";
            yield break;
        }

        if (payload.Subject is not null && string.IsNullOrWhiteSpace(payload.Subject))
        {
            yield return "Subject cannot be empty.";
            yield break;
        }

        if (payload.Topic is not null && string.IsNullOrWhiteSpace(payload.Topic))
        {
            yield return "Topic cannot be empty.";
            yield break;
        }

        if (payload.Difficulty is not null && string.IsNullOrWhiteSpace(payload.Difficulty))
        {
            yield return "Difficulty cannot be empty.";
            yield break;
        }

        if (payload.Grade.HasValue && !ApiEndpointHelpers.IsGradeInRange(payload.Grade.Value))
        {
            yield return "Grade must be between 1 and 12.";
        }
    }
}