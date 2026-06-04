public sealed class DeleteParentLessonCommandValidator : IRequestValidator<DeleteParentLessonCommand>
{
    public IEnumerable<string> Validate(DeleteParentLessonCommand request)
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