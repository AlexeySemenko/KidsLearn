using MediatR;
using Microsoft.EntityFrameworkCore;

public sealed record LoginChildCommand(ChildLoginRequest Request) : IRequest<LoginChildResult>;

public sealed record LoginChildResult(AuthTokenResponse? Response, string? Error, int StatusCode)
{
    public static LoginChildResult BadRequest(string error)
        => new(null, error, StatusCodes.Status400BadRequest);

    public static LoginChildResult Unauthorized()
        => new(null, null, StatusCodes.Status401Unauthorized);

    public static LoginChildResult Success(AuthTokenResponse response)
        => new(response, null, StatusCodes.Status200OK);
}

public sealed class LoginChildCommandHandler : IRequestHandler<LoginChildCommand, LoginChildResult>
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasherService _passwordHasher;
    private readonly IJwtTokenService _tokenService;
    private readonly IConfiguration _configuration;

    public LoginChildCommandHandler(
        AppDbContext db,
        IPasswordHasherService passwordHasher,
        IJwtTokenService tokenService,
        IConfiguration configuration)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _configuration = configuration;
    }

    public async Task<LoginChildResult> Handle(LoginChildCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;
        if (request.ChildId == Guid.Empty || string.IsNullOrWhiteSpace(request.AccessCode))
        {
            return LoginChildResult.BadRequest("ChildId and access code are required.");
        }

        var child = await _db.Children
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.Id == request.ChildId, cancellationToken);

        if (child?.User is null || child.User.Role != UserRole.Child)
        {
            return LoginChildResult.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(child.User.PasswordHash)
            || !_passwordHasher.VerifyPassword(request.AccessCode.Trim(), child.User.PasswordHash))
        {
            return LoginChildResult.Unauthorized();
        }

        var accessToken = _tokenService.CreateAccessToken(child.User);
        var refreshToken = _tokenService.CreateRefreshToken();
        var refreshExpiresDays = int.TryParse(_configuration["Jwt:RefreshTokenExpirationDays"], out var refreshDays)
            ? refreshDays
            : 14;

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = child.User.Id,
            Token = refreshToken,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(refreshExpiresDays)
        });

        await _db.SaveChangesAsync(cancellationToken);

        var expiresIn = int.TryParse(_configuration["Jwt:AccessTokenExpirationMinutes"], out var accessMinutes)
            ? accessMinutes * 60
            : 1800;

        return LoginChildResult.Success(new AuthTokenResponse(
            accessToken,
            refreshToken,
            expiresIn,
            new AuthUserResponse(child.User.Id, child.User.Email, child.User.Role.ToString(), child.Name, child.User.AvatarUrl)));
    }
}