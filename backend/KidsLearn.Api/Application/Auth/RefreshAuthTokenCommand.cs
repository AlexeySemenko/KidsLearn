using MediatR;
using Microsoft.EntityFrameworkCore;

public sealed record RefreshAuthTokenCommand(RefreshRequest Request) : IRequest<RefreshAuthTokenResult>;

public sealed record RefreshAuthTokenResult(AuthTokenResponse? Response, string? Error, int StatusCode)
{
    public static RefreshAuthTokenResult BadRequest(string error)
        => new(null, error, StatusCodes.Status400BadRequest);

    public static RefreshAuthTokenResult Unauthorized()
        => new(null, null, StatusCodes.Status401Unauthorized);

    public static RefreshAuthTokenResult Success(AuthTokenResponse response)
        => new(response, null, StatusCodes.Status200OK);
}

public sealed class RefreshAuthTokenCommandHandler : IRequestHandler<RefreshAuthTokenCommand, RefreshAuthTokenResult>
{
    private readonly AppDbContext _db;
    private readonly IJwtTokenService _tokenService;
    private readonly IConfiguration _configuration;

    public RefreshAuthTokenCommandHandler(AppDbContext db, IJwtTokenService tokenService, IConfiguration configuration)
    {
        _db = db;
        _tokenService = tokenService;
        _configuration = configuration;
    }

    public async Task<RefreshAuthTokenResult> Handle(RefreshAuthTokenCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return RefreshAuthTokenResult.BadRequest("Refresh token is required.");
        }

        var existing = await _db.RefreshTokens
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.Token == request.RefreshToken, cancellationToken);

        if (existing is null || existing.RevokedAt.HasValue || existing.ExpiresAt <= DateTime.UtcNow)
        {
            return RefreshAuthTokenResult.Unauthorized();
        }

        existing.RevokedAt = DateTime.UtcNow;

        var newRefreshToken = _tokenService.CreateRefreshToken();
        var refreshExpiresDays = int.TryParse(_configuration["Jwt:RefreshTokenExpirationDays"], out var refreshDays)
            ? refreshDays
            : 14;

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = existing.UserId,
            Token = newRefreshToken,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(refreshExpiresDays)
        });

        var accessToken = _tokenService.CreateAccessToken(existing.User);
        await _db.SaveChangesAsync(cancellationToken);

        var expiresIn = int.TryParse(_configuration["Jwt:AccessTokenExpirationMinutes"], out var accessMinutes)
            ? accessMinutes * 60
            : 1800;

        string? displayName;
        if (existing.User.Role == UserRole.Child)
        {
            var childName = await _db.Children
                .AsNoTracking()
                .Where(x => x.UserId == existing.UserId)
                .Select(x => x.Name)
                .FirstOrDefaultAsync(cancellationToken);
            displayName = string.IsNullOrWhiteSpace(childName) ? existing.User.Email : childName;
        }
        else
        {
            displayName = existing.User.Email;
        }

        return RefreshAuthTokenResult.Success(new AuthTokenResponse(
            accessToken,
            newRefreshToken,
            expiresIn,
            new AuthUserResponse(existing.User.Id, existing.User.Email, existing.User.Role.ToString(), displayName, existing.User.AvatarUrl)));
    }
}