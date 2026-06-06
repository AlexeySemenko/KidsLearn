public sealed class DuplicateParentLessonCommandValidator : IRequestValidator<DuplicateParentLessonCommand>
{
    public IEnumerable<string> Validate(DuplicateParentLessonCommand request)
    {
        if (request.ParentId == Guid.Empty)
        {
            yield return "Parent id is required.";
            yield break;
        }

        if (request.LessonId == Guid.Empty)
        {
            yield return "Lesson id is required.";
        }
    }
}