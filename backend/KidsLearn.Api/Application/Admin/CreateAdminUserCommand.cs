using MediatR;
using Microsoft.EntityFrameworkCore;

public sealed record CreateAdminUserCommand(AdminCreateUserRequest Request, string CallerEmail) : IRequest<CreateAdminUserResult>;

public sealed record CreateAdminUserResult(AdminUserResponse? User, bool EmailSent, string? Error, int StatusCode)
{
    public static CreateAdminUserResult BadRequest(string error)
        => new(null, false, error, StatusCodes.Status400BadRequest);

    public static CreateAdminUserResult Conflict(string error)
        => new(null, false, error, StatusCodes.Status409Conflict);

    public static CreateAdminUserResult Created(AdminUserResponse user, bool emailSent)
        => new(user, emailSent, null, StatusCodes.Status201Created);
}

public sealed class CreateAdminUserCommandHandler : IRequestHandler<CreateAdminUserCommand, CreateAdminUserResult>
{
    private readonly AppDbContext _db;
    private readonly IEmailService _emailService;

    public CreateAdminUserCommandHandler(AppDbContext db, IEmailService emailService)
    {
        _db = db;
        _emailService = emailService;
    }

    public async Task<CreateAdminUserResult> Handle(CreateAdminUserCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;

        if (string.IsNullOrWhiteSpace(request.Email))
            return CreateAdminUserResult.BadRequest("Email is required.");

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        if (!Enum.TryParse<UserRole>(request.Role, ignoreCase: true, out var role))
            return CreateAdminUserResult.BadRequest($"Invalid role '{request.Role}'. Valid values: Parent, Child, Admin.");

        var existing = await _db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);
        if (existing is not null)
            return CreateAdminUserResult.Conflict("A user with this email already exists.");

        var user = new AppUser
        {
            Email = normalizedEmail,
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? null : request.DisplayName.Trim(),
            PasswordHash = string.Empty,
            Role = role,
            EmailVerified = false,
            CreatedAt = DateTime.UtcNow,
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(cancellationToken);

        var emailSent = await _emailService.SendInvitationAsync(user.Email, user.DisplayName, command.CallerEmail);

        var response = new AdminUserResponse(
            user.Id, user.Email, user.DisplayName, user.Role.ToString(),
            user.EmailVerified, user.ExternalProvider, user.CreatedAt, user.LastAccessAt);

        return CreateAdminUserResult.Created(response, emailSent);
    }
}
