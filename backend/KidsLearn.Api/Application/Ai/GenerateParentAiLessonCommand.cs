using MediatR;

public sealed record GenerateParentAiLessonCommand(Guid ParentId, GenerateAiLessonRequest Request)
    : IRequest<GenerateParentAiLessonResult>;

public sealed record GenerateParentAiLessonResult(GenerateAiLessonResponse? Response, string? Error, int StatusCode)
{
    public static GenerateParentAiLessonResult FromServiceResult(ServiceResult<GenerateAiLessonResponse> serviceResult)
        => new(serviceResult.Value, serviceResult.Error, serviceResult.StatusCode);
}

public sealed class GenerateParentAiLessonCommandHandler
    : IRequestHandler<GenerateParentAiLessonCommand, GenerateParentAiLessonResult>
{
    private readonly IAiLessonGenerationService _aiLessonGenerationService;

    public GenerateParentAiLessonCommandHandler(IAiLessonGenerationService aiLessonGenerationService)
    {
        _aiLessonGenerationService = aiLessonGenerationService;
    }

    public async Task<GenerateParentAiLessonResult> Handle(GenerateParentAiLessonCommand command, CancellationToken cancellationToken)
    {
        var result = await _aiLessonGenerationService.GenerateAndPersistAsync(command.ParentId, command.Request, cancellationToken);
        return GenerateParentAiLessonResult.FromServiceResult(result);
    }
}