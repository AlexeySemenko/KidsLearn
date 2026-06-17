using MediatR;

public sealed record DeleteAdminUserCommand(Guid UserId, Guid CallerId) : IRequest<DeleteAdminUserResult>;

public sealed record DeleteAdminUserResult(string? Error, int StatusCode)
{
    public static DeleteAdminUserResult BadRequest(string error)
        => new(error, StatusCodes.Status400BadRequest);

    public static DeleteAdminUserResult NotFound(string error)
        => new(error, StatusCodes.Status404NotFound);

    public static DeleteAdminUserResult NoContent()
        => new(null, StatusCodes.Status204NoContent);
}

public sealed class DeleteAdminUserCommandHandler : IRequestHandler<DeleteAdminUserCommand, DeleteAdminUserResult>
{
    private readonly AppDbContext _db;

    public DeleteAdminUserCommandHandler(AppDbContext db)
    {
        _db = db;
    }

    public async Task<DeleteAdminUserResult> Handle(DeleteAdminUserCommand command, CancellationToken cancellationToken)
    {
        if (command.CallerId == command.UserId)
            return DeleteAdminUserResult.BadRequest("You cannot delete your own account.");

        var user = await _db.Users.FindAsync([command.UserId], cancellationToken);
        if (user is null)
            return DeleteAdminUserResult.NotFound("User not found.");

        _db.Users.Remove(user);
        await _db.SaveChangesAsync(cancellationToken);

        return DeleteAdminUserResult.NoContent();
    }
}
