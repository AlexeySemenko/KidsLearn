using MediatR;
using Microsoft.EntityFrameworkCore;

public sealed record RegisterParentCommand(RegisterRequest Request) : IRequest<RegisterParentResult>;

public sealed record RegisterParentResult(AuthUserResponse? User, string? Error, int StatusCode)
{
    public static RegisterParentResult BadRequest(string error)
        => new(null, error, StatusCodes.Status400BadRequest);

    public static RegisterParentResult Conflict(string error)
        => new(null, error, StatusCodes.Status409Conflict);

    public static RegisterParentResult Created(AuthUserResponse user)
        => new(user, null, StatusCodes.Status201Created);
}

public sealed class RegisterParentCommandHandler : IRequestHandler<RegisterParentCommand, RegisterParentResult>
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasherService _passwordHasher;
    private readonly IEmailService _emailService;

    public RegisterParentCommandHandler(AppDbContext db, IPasswordHasherService passwordHasher, IEmailService emailService)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _emailService = emailService;
    }

    public async Task<RegisterParentResult> Handle(RegisterParentCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;

        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return RegisterParentResult.BadRequest("Email and password are required.");
        }

        var email = request.Email.Trim().ToLowerInvariant();
        if (!email.Contains('@'))
        {
            return RegisterParentResult.BadRequest("Email format is invalid.");
        }

        if (request.Password.Length < 8)
        {
            return RegisterParentResult.BadRequest("Password must be at least 8 characters.");
        }

        var exists = await _db.Users.AnyAsync(x => x.Email == email, cancellationToken);
        if (exists)
        {
            return RegisterParentResult.Conflict("User with this email already exists.");
        }

        var user = new AppUser
        {
            Email = email,
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            Role = UserRole.Parent,
            CreatedAt = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(cancellationToken);

        var emailService = _emailService;
        _ = Task.Run(async () =>
        {
            try { await emailService.SendWelcomeToParentAsync(email, null); } catch { }
        });

        return RegisterParentResult.Created(new AuthUserResponse(user.Id, user.Email, user.Role.ToString(), user.Email, null));
    }
}
