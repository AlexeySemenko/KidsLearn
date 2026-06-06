using Microsoft.AspNetCore.Http;
using Xunit;

public class AiCommandHandlersUnitTests
{
    [Fact]
    public async Task GenerateParentAiLessonCommand_ReturnsMappedSuccessResult()
    {
        var expectedResponse = new GenerateAiLessonResponse(
            Guid.NewGuid(),
            new LessonDetailResponse(
                Guid.NewGuid(),
                "Generated",
                "Math",
                3,
                "Addition",
                "Easy",
                DateTime.UtcNow,
                new List<QuestionResponse>()),
            new AiProviderMetaResponse("Fake", "model", false, null));

        var service = new FakeAiLessonGenerationService(ServiceResult<GenerateAiLessonResponse>.Success(expectedResponse));
        var handler = new GenerateParentAiLessonCommandHandler(service);

        var result = await handler.Handle(
            new GenerateParentAiLessonCommand(
                Guid.NewGuid(),
                new GenerateAiLessonRequest("Math", 3, "Addition", 3, "Easy", "en", null)),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.NotNull(result.Response);
        Assert.Equal(expectedResponse.CreatedLessonId, result.Response!.CreatedLessonId);
    }

    [Fact]
    public async Task GenerateParentAiLessonCommand_PropagatesUnprocessableEntity()
    {
        var service = new FakeAiLessonGenerationService(
            ServiceResult<GenerateAiLessonResponse>.Fail(StatusCodes.Status422UnprocessableEntity, "AI schema validation failed."));

        var handler = new GenerateParentAiLessonCommandHandler(service);
        var result = await handler.Handle(
            new GenerateParentAiLessonCommand(
                Guid.NewGuid(),
                new GenerateAiLessonRequest("Math", 3, "Addition", 3, "Easy", "en", null)),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, result.StatusCode);
        Assert.Equal("AI schema validation failed.", result.Error);
    }

    [Fact]
    public async Task EditParentAiLessonCommand_ReturnsMappedSuccessResult()
    {
        var expected = new EditAiLessonResponse(
            Guid.NewGuid(),
            2,
            "Difficulty changed to 'Hard'.",
            new LessonDetailResponse(
                Guid.NewGuid(),
                "Lesson",
                "Math",
                4,
                "Fractions",
                "Hard",
                DateTime.UtcNow,
                new List<QuestionResponse>()));

        var service = new FakeAiLessonEditingService(ServiceResult<EditAiLessonResponse>.Success(expected));
        var handler = new EditParentAiLessonCommandHandler(service);

        var result = await handler.Handle(
            new EditParentAiLessonCommand(
                Guid.NewGuid(),
                Guid.NewGuid(),
                new EditAiLessonRequest("change-difficulty", new Dictionary<string, string> { ["difficulty"] = "Hard" }, null)),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.NotNull(result.Response);
        Assert.Equal(expected.RevisionNumber, result.Response!.RevisionNumber);
    }

    [Fact]
    public async Task EditParentAiLessonCommand_PropagatesBadRequest()
    {
        var service = new FakeAiLessonEditingService(
            ServiceResult<EditAiLessonResponse>.Fail(StatusCodes.Status400BadRequest, "Unsupported command."));

        var handler = new EditParentAiLessonCommandHandler(service);
        var result = await handler.Handle(
            new EditParentAiLessonCommand(
                Guid.NewGuid(),
                Guid.NewGuid(),
                new EditAiLessonRequest("unsupported", null, null)),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("Unsupported command.", result.Error);
    }

    private sealed class FakeAiLessonGenerationService : IAiLessonGenerationService
    {
        private readonly ServiceResult<GenerateAiLessonResponse> _result;

        public FakeAiLessonGenerationService(ServiceResult<GenerateAiLessonResponse> result)
        {
            _result = result;
        }

        public Task<ServiceResult<GenerateAiLessonResponse>> GenerateAndPersistAsync(
            Guid parentId,
            GenerateAiLessonRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_result);
        }
    }

    private sealed class FakeAiLessonEditingService : IAiLessonEditingService
    {
        private readonly ServiceResult<EditAiLessonResponse> _result;

        public FakeAiLessonEditingService(ServiceResult<EditAiLessonResponse> result)
        {
            _result = result;
        }

        public Task<ServiceResult<EditAiLessonResponse>> EditAsync(
            Guid parentId,
            Guid lessonId,
            EditAiLessonRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_result);
        }
    }
}
