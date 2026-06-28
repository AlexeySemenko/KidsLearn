using MediatR;
using Microsoft.EntityFrameworkCore;

public sealed record RegisterChildCommand(RegisterChildRequest Request) : IRequest<RegisterChildResult>;

public sealed record RegisterChildResult(AuthTokenResponse? Response, string? Error, int StatusCode)
{
    public static RegisterChildResult BadRequest(string error)
        => new(null, error, StatusCodes.Status400BadRequest);

    public static RegisterChildResult Conflict(string error)
        => new(null, error, StatusCodes.Status409Conflict);

    public static RegisterChildResult Created(AuthTokenResponse response)
        => new(response, null, StatusCodes.Status201Created);
}

public sealed class RegisterChildCommandHandler : IRequestHandler<RegisterChildCommand, RegisterChildResult>
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasherService _passwordHasher;
    private readonly IJwtTokenService _tokenService;
    private readonly IConfiguration _configuration;

    public RegisterChildCommandHandler(
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

    public async Task<RegisterChildResult> Handle(RegisterChildCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;

        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return RegisterChildResult.BadRequest("Email and password are required.");

        if (request.Password.Length < 8)
            return RegisterChildResult.BadRequest("Password must be at least 8 characters.");

        var email = request.Email.Trim().ToLowerInvariant();

        // Find pending enrollment: a Child record with this email that has no linked user yet
        var child = await _db.Children
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.EnrollmentEmail == email, cancellationToken);

        if (child is null)
            return RegisterChildResult.BadRequest("Your parent has to enroll you to the system first.");

        if (child.UserId is not null)
            return RegisterChildResult.Conflict("This account is already set up. Please sign in.");

        // Create the AppUser now
        var childUser = new AppUser
        {
            Email = email,
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            Role = UserRole.Child,
            EmailVerified = false,
            CreatedAt = DateTime.UtcNow,
            LastAccessAt = DateTime.UtcNow,
        };

        _db.Users.Add(childUser);

        child.UserId = childUser.Id;
        child.User = childUser;

        var accessToken = _tokenService.CreateAccessToken(childUser);
        var refreshToken = _tokenService.CreateRefreshToken();
        var refreshExpiresDays = int.TryParse(_configuration["Jwt:RefreshTokenExpirationDays"], out var refreshDays)
            ? refreshDays : 14;

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = childUser.Id,
            Token = refreshToken,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(refreshExpiresDays)
        });

        await _db.SaveChangesAsync(cancellationToken);

        var expiresIn = int.TryParse(_configuration["Jwt:AccessTokenExpirationMinutes"], out var mins)
            ? mins * 60 : 1800;

        return RegisterChildResult.Created(new AuthTokenResponse(
            accessToken, refreshToken, expiresIn,
            new AuthUserResponse(childUser.Id, childUser.Email, childUser.Role.ToString(), child.Name, childUser.AvatarUrl)));
    }
}
