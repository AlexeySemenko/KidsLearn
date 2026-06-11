using MediatR;
using Microsoft.EntityFrameworkCore;

public sealed record ResetParentChildAccessCodeCommand(Guid ParentId, Guid ChildId) : IRequest<ResetParentChildAccessCodeResult>;

public sealed record ResetParentChildAccessCodeResult(ResetChildAccessCodeResponse? Response, int StatusCode)
{
    public static ResetParentChildAccessCodeResult NotFound()
        => new(null, StatusCodes.Status404NotFound);

    public static ResetParentChildAccessCodeResult Success(ResetChildAccessCodeResponse response)
        => new(response, StatusCodes.Status200OK);
}

public sealed class ResetParentChildAccessCodeCommandHandler : IRequestHandler<ResetParentChildAccessCodeCommand, ResetParentChildAccessCodeResult>
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasherService _passwordHasher;

    public ResetParentChildAccessCodeCommandHandler(AppDbContext db, IPasswordHasherService passwordHasher)
    {
        _db = db;
        _passwordHasher = passwordHasher;
    }

    public async Task<ResetParentChildAccessCodeResult> Handle(ResetParentChildAccessCodeCommand command, CancellationToken cancellationToken)
    {
        var scopedParentIds = await ApiEndpointHelpers.ResolveParentScopeIdsAsync(_db, command.ParentId);

        var child = await _db.Children.FirstOrDefaultAsync(
            x => x.Id == command.ChildId && scopedParentIds.Contains(x.ParentId),
            cancellationToken);

        if (child is null || !child.UserId.HasValue)
        {
            return ResetParentChildAccessCodeResult.NotFound();
        }

        var childUser = await _db.Users.FirstOrDefaultAsync(
            x => x.Id == child.UserId.Value && x.Role == UserRole.Child,
            cancellationToken);

        if (childUser is null)
        {
            return ResetParentChildAccessCodeResult.NotFound();
        }

        var newCode = ApiEndpointHelpers.GenerateAccessCode();
        childUser.PasswordHash = _passwordHasher.HashPassword(newCode);
        await _db.SaveChangesAsync(cancellationToken);

        return ResetParentChildAccessCodeResult.Success(new ResetChildAccessCodeResponse(child.Id, newCode));
    }
}