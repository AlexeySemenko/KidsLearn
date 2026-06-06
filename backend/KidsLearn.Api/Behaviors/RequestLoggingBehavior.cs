using MediatR;

public sealed class RequestLoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<RequestLoggingBehavior<TRequest, TResponse>> _logger;

    public RequestLoggingBehavior(ILogger<RequestLoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var startedAt = DateTime.UtcNow;

        _logger.LogInformation("Handling {RequestName}", requestName);

        try
        {
            var response = await next();
            var elapsedMs = (DateTime.UtcNow - startedAt).TotalMilliseconds;

            _logger.LogInformation("Handled {RequestName} in {ElapsedMs:0.00} ms", requestName, elapsedMs);
            return response;
        }
        catch (Exception ex)
        {
            var elapsedMs = (DateTime.UtcNow - startedAt).TotalMilliseconds;
            _logger.LogError(ex, "Request {RequestName} failed after {ElapsedMs:0.00} ms", requestName, elapsedMs);
            throw;
        }
    }
}