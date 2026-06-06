using Microsoft.AspNetCore.Http;
using Xunit;

public class ValidationBehaviorUnitTests
{
    [Fact]
    public async Task Handle_ReturnsValidationFailureResponse_WhenValidatorFailsAndFactoryExists()
    {
        var behavior = new ValidationBehavior<TestRequestWithFactory, TestResponseWithFactory>(
            new IRequestValidator<TestRequestWithFactory>[]
            {
                new AlwaysFailingFactoryValidator()
            });

        var result = await behavior.Handle(
            new TestRequestWithFactory(),
            () => Task.FromResult(new TestResponseWithFactory("next")),
            CancellationToken.None);

        Assert.Equal("failure", result.Message);
    }

    [Fact]
    public async Task Handle_Throws_WhenValidatorFailsAndFactoryIsMissing()
    {
        var behavior = new ValidationBehavior<TestRequestWithoutFactory, TestResponseWithoutFactory>(
            new IRequestValidator<TestRequestWithoutFactory>[]
            {
                new AlwaysFailingNoFactoryValidator()
            });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await behavior.Handle(
                new TestRequestWithoutFactory(),
                () => Task.FromResult(new TestResponseWithoutFactory("next")),
                CancellationToken.None));

        Assert.Contains(nameof(TestRequestWithoutFactory), exception.Message);
    }

    [Fact]
    public async Task Handle_CallsNext_WhenAllValidatorsPass()
    {
        var behavior = new ValidationBehavior<TestRequestWithoutFactory, TestResponseWithoutFactory>(
            new IRequestValidator<TestRequestWithoutFactory>[]
            {
                new PassingValidator()
            });

        var result = await behavior.Handle(
            new TestRequestWithoutFactory(),
            () => Task.FromResult(new TestResponseWithoutFactory("next")),
            CancellationToken.None);

        Assert.Equal("next", result.Message);
    }

    [Fact]
    public void CreateParentLessonCommandValidator_ReturnsError_WhenQuestionsMissing()
    {
        var validator = new CreateParentLessonCommandValidator();
        var request = new CreateParentLessonCommand(
            Guid.NewGuid(),
            new CreateLessonRequest("", "Math", 3, "Addition", "Easy", new List<CreateQuestionRequest>()));

        var error = validator.Validate(request).First();

        Assert.Equal("Title, subject, topic and grade (1-12) are required.", error);
    }

    [Fact]
    public void SubmitParentAssignmentAnswersCommandValidator_ReturnsError_WhenAnswersMissing()
    {
        var validator = new SubmitParentAssignmentAnswersCommandValidator();
        var request = new SubmitParentAssignmentAnswersCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new SubmitAssignmentAnswersRequest(new List<SubmitAnswerRequest>()));

        var error = validator.Validate(request).First();

        Assert.Equal("At least one answer is required.", error);
    }

    [Fact]
    public void DeleteParentLessonCommandValidator_ReturnsError_WhenParentIdMissing()
    {
        var validator = new DeleteParentLessonCommandValidator();

        var error = validator.Validate(new DeleteParentLessonCommand(Guid.Empty, Guid.NewGuid())).First();

        Assert.Equal("Parent id is required.", error);
    }

    [Fact]
    public void DeleteParentLessonCommandValidator_ReturnsError_WhenLessonIdMissing()
    {
        var validator = new DeleteParentLessonCommandValidator();

        var error = validator.Validate(new DeleteParentLessonCommand(Guid.NewGuid(), Guid.Empty)).First();

        Assert.Equal("Lesson id is required.", error);
    }

    [Fact]
    public void UpdateParentLessonCommandValidator_ReturnsError_WhenPayloadTitleEmpty()
    {
        var validator = new UpdateParentLessonCommandValidator();

        var error = validator.Validate(
            new UpdateParentLessonCommand(
                Guid.NewGuid(),
                Guid.NewGuid(),
                new UpdateLessonRequest("", null, null, null, null))).First();

        Assert.Equal("Title cannot be empty.", error);
    }

    [Fact]
    public void UpdateParentLessonCommandValidator_ReturnsError_WhenPayloadGradeOutOfRange()
    {
        var validator = new UpdateParentLessonCommandValidator();

        var error = validator.Validate(
            new UpdateParentLessonCommand(
                Guid.NewGuid(),
                Guid.NewGuid(),
                new UpdateLessonRequest(null, null, 0, null, null))).First();

        Assert.Equal("Grade must be between 1 and 12.", error);
    }

    private sealed record TestRequestWithFactory : IValidationFailureResponseFactory<TestResponseWithFactory>
    {
        public TestResponseWithFactory CreateValidationFailureResponse(string error)
        {
            return new TestResponseWithFactory(error);
        }
    }

    private sealed record TestRequestWithoutFactory;

    private sealed record TestResponseWithFactory(string Message);

    private sealed record TestResponseWithoutFactory(string Message);

    private sealed class AlwaysFailingFactoryValidator : IRequestValidator<TestRequestWithFactory>
    {
        public IEnumerable<string> Validate(TestRequestWithFactory request)
        {
            yield return "failure";
        }
    }

    private sealed class AlwaysFailingNoFactoryValidator : IRequestValidator<TestRequestWithoutFactory>
    {
        public IEnumerable<string> Validate(TestRequestWithoutFactory request)
        {
            yield return "failure";
        }
    }

    private sealed class PassingValidator : IRequestValidator<TestRequestWithoutFactory>
    {
        public IEnumerable<string> Validate(TestRequestWithoutFactory request)
        {
            yield break;
        }
    }
}