using MediatR;

public sealed record EditParentAiLessonCommand(Guid ParentId, Guid LessonId, EditAiLessonRequest Request)
    : IRequest<EditParentAiLessonResult>;

public sealed record EditParentAiLessonResult(EditAiLessonResponse? Response, string? Error, int StatusCode)
{
    public static EditParentAiLessonResult FromServiceResult(ServiceResult<EditAiLessonResponse> serviceResult)
        => new(serviceResult.Value, serviceResult.Error, serviceResult.StatusCode);
}

public sealed class EditParentAiLessonCommandHandler
    : IRequestHandler<EditParentAiLessonCommand, EditParentAiLessonResult>
{
    private readonly IAiLessonEditingService _aiLessonEditingService;

    public EditParentAiLessonCommandHandler(IAiLessonEditingService aiLessonEditingService)
    {
        _aiLessonEditingService = aiLessonEditingService;
    }

    public async Task<EditParentAiLessonResult> Handle(EditParentAiLessonCommand command, CancellationToken cancellationToken)
    {
        var result = await _aiLessonEditingService.EditAsync(command.ParentId, command.LessonId, command.Request, cancellationToken);
        return EditParentAiLessonResult.FromServiceResult(result);
    }
}