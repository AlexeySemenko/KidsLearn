using MediatR;

public sealed record UpdateAdminUserCommand(Guid UserId, AdminUpdateUserRequest Request) : IRequest<UpdateAdminUserResult>;

public sealed record UpdateAdminUserResult(AdminUserResponse? User, string? Error, int StatusCode)
{
    public static UpdateAdminUserResult BadRequest(string error)
        => new(null, error, StatusCodes.Status400BadRequest);

    public static UpdateAdminUserResult NotFound(string error)
        => new(null, error, StatusCodes.Status404NotFound);

    public static UpdateAdminUserResult Ok(AdminUserResponse user)
        => new(user, null, StatusCodes.Status200OK);
}

public sealed class UpdateAdminUserCommandHandler : IRequestHandler<UpdateAdminUserCommand, UpdateAdminUserResult>
{
    private readonly AppDbContext _db;

    public UpdateAdminUserCommandHandler(AppDbContext db)
    {
        _db = db;
    }

    public async Task<UpdateAdminUserResult> Handle(UpdateAdminUserCommand command, CancellationToken cancellationToken)
    {
        var user = await _db.Users.FindAsync([command.UserId], cancellationToken);
        if (user is null)
            return UpdateAdminUserResult.NotFound("User not found.");

        var request = command.Request;

        if (request.DisplayName is not null)
            user.DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? null : request.DisplayName.Trim();

        if (!string.IsNullOrWhiteSpace(request.Role))
        {
            if (!Enum.TryParse<UserRole>(request.Role, ignoreCase: true, out var newRole))
                return UpdateAdminUserResult.BadRequest($"Invalid role '{request.Role}'.");
            user.Role = newRole;
        }

        await _db.SaveChangesAsync(cancellationToken);

        var response = new AdminUserResponse(
            user.Id, user.Email, user.DisplayName, user.Role.ToString(),
            user.EmailVerified, user.ExternalProvider, user.CreatedAt, user.LastAccessAt);

        return UpdateAdminUserResult.Ok(response);
    }
}
