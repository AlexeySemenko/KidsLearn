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
    private readonly IEmailService _emailService;

    public RegisterChildCommandHandler(
        AppDbContext db,
        IPasswordHasherService passwordHasher,
        IJwtTokenService tokenService,
        IConfiguration configuration,
        IEmailService emailService)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _configuration = configuration;
        _emailService = emailService;
    }

    public async Task<RegisterChildResult> Handle(RegisterChildCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;

        if (string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.Password))
            return RegisterChildResult.BadRequest("Registration token and password are required.");

        if (request.Password.Length < 8)
            return RegisterChildResult.BadRequest("Password must be at least 8 characters.");

        // Find pending enrollment by the secure registration token
        var child = await _db.Children
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.RegistrationToken == request.Token, cancellationToken);

        if (child is null)
            return RegisterChildResult.BadRequest("This registration link is invalid or has already been used.");

        if (child.UserId is not null)
            return RegisterChildResult.Conflict("This account is already set up. Please sign in.");

        if (string.IsNullOrWhiteSpace(child.EnrollmentEmail))
            return RegisterChildResult.BadRequest("Enrollment record is missing an email address.");

        var email = child.EnrollmentEmail;

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

        // Pre-fetch before fire-and-forget so DbContext is not accessed after request ends
        var childName = child.Name;
        var parentId = child.ParentId;
        var linkedParentIds = await _db.ParentAccountLinks
            .AsNoTracking()
            .Where(x => x.ParentAId == parentId || x.ParentBId == parentId)
            .Select(x => x.ParentAId == parentId ? x.ParentBId : x.ParentAId)
            .ToListAsync(cancellationToken);
        var allParentIds = new HashSet<Guid>(linkedParentIds) { parentId };
        var parentRecipients = await _db.Users
            .AsNoTracking()
            .Where(u => allParentIds.Contains(u.Id))
            .Select(u => new { u.Email, Name = u.DisplayName ?? u.Email })
            .ToListAsync(cancellationToken);
        var emailService = _emailService;

        _ = Task.Run(async () =>
        {
            foreach (var parent in parentRecipients)
            {
                try { await emailService.SendChildRegisteredToParentAsync(parent.Email, parent.Name, childName); }
                catch { }
            }
        });

        return RegisterChildResult.Created(new AuthTokenResponse(
            accessToken, refreshToken, expiresIn,
            new AuthUserResponse(childUser.Id, childUser.Email, childUser.Role.ToString(), child.Name, childUser.AvatarUrl)));
    }
}
