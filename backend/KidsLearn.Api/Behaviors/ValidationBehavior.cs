using MediatR;

public interface IRequestValidator<in TRequest>
{
    IEnumerable<string> Validate(TRequest request);
}

public interface IValidationFailureResponseFactory<TResponse>
{
    TResponse CreateValidationFailureResponse(string error);
}

public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IRequestValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IRequestValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        foreach (var validator in _validators)
        {
            var validationError = validator.Validate(request).FirstOrDefault();
            if (validationError is null)
            {
                continue;
            }

            if (request is IValidationFailureResponseFactory<TResponse> failureFactory)
            {
                return Task.FromResult(failureFactory.CreateValidationFailureResponse(validationError));
            }

            throw new InvalidOperationException($"Validation failed for {typeof(TRequest).Name}, but no failure response factory was provided.");
        }

        return next();
    }
}