using MediatR;
using Microsoft.EntityFrameworkCore;

public sealed record LoginParentCommand(LoginRequest Request) : IRequest<LoginParentResult>;

public sealed record LoginParentResult(AuthTokenResponse? Response, string? Error, int StatusCode)
{
    public static LoginParentResult BadRequest(string error)
        => new(null, error, StatusCodes.Status400BadRequest);

    public static LoginParentResult Unauthorized()
        => new(null, null, StatusCodes.Status401Unauthorized);

    public static LoginParentResult Success(AuthTokenResponse response)
        => new(response, null, StatusCodes.Status200OK);
}

public sealed class LoginParentCommandHandler : IRequestHandler<LoginParentCommand, LoginParentResult>
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasherService _passwordHasher;
    private readonly IJwtTokenService _tokenService;
    private readonly IConfiguration _configuration;

    public LoginParentCommandHandler(
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

    public async Task<LoginParentResult> Handle(LoginParentCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return LoginParentResult.BadRequest("Email and password are required.");
        }

        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(x => x.Email == email, cancellationToken);
        if (user is null || string.IsNullOrWhiteSpace(user.PasswordHash) || !_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            return LoginParentResult.Unauthorized();
        }

        var accessToken = _tokenService.CreateAccessToken(user);
        var refreshToken = _tokenService.CreateRefreshToken();
        var refreshExpiresDays = int.TryParse(_configuration["Jwt:RefreshTokenExpirationDays"], out var refreshDays)
            ? refreshDays
            : 14;

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            Token = refreshToken,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(refreshExpiresDays)
        });

        await _db.SaveChangesAsync(cancellationToken);

        var expiresIn = int.TryParse(_configuration["Jwt:AccessTokenExpirationMinutes"], out var accessMinutes)
            ? accessMinutes * 60
            : 1800;

        return LoginParentResult.Success(new AuthTokenResponse(
            accessToken,
            refreshToken,
            expiresIn,
            new AuthUserResponse(user.Id, user.Email, user.Role.ToString())));
    }
}