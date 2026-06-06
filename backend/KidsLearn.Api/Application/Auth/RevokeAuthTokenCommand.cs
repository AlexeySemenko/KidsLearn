using MediatR;
using Microsoft.EntityFrameworkCore;

public sealed record RevokeAuthTokenCommand(RevokeRequest Request) : IRequest<RevokeAuthTokenResult>;

public sealed record RevokeAuthTokenResult(int StatusCode, string? Error)
{
    public static RevokeAuthTokenResult BadRequest(string error)
        => new(StatusCodes.Status400BadRequest, error);

    public static RevokeAuthTokenResult NotFound()
        => new(StatusCodes.Status404NotFound, null);

    public static RevokeAuthTokenResult NoContent()
        => new(StatusCodes.Status204NoContent, null);
}

public sealed class RevokeAuthTokenCommandHandler : IRequestHandler<RevokeAuthTokenCommand, RevokeAuthTokenResult>
{
    private readonly AppDbContext _db;

    public RevokeAuthTokenCommandHandler(AppDbContext db)
    {
        _db = db;
    }

    public async Task<RevokeAuthTokenResult> Handle(RevokeAuthTokenCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return RevokeAuthTokenResult.BadRequest("Refresh token is required.");
        }

        var existing = await _db.RefreshTokens.FirstOrDefaultAsync(x => x.Token == request.RefreshToken, cancellationToken);
        if (existing is null)
        {
            return RevokeAuthTokenResult.NotFound();
        }

        if (!existing.RevokedAt.HasValue)
        {
            existing.RevokedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }

        return RevokeAuthTokenResult.NoContent();
    }
}