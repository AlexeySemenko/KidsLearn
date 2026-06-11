using MediatR;
using Microsoft.EntityFrameworkCore;

public sealed record DeleteParentChildCommand(Guid ParentId, Guid ChildId) : IRequest<DeleteParentChildResult>;

public sealed record DeleteParentChildResult(int StatusCode)
{
    public static DeleteParentChildResult NotFound()
        => new(StatusCodes.Status404NotFound);

    public static DeleteParentChildResult NoContent()
        => new(StatusCodes.Status204NoContent);
}

public sealed class DeleteParentChildCommandHandler : IRequestHandler<DeleteParentChildCommand, DeleteParentChildResult>
{
    private readonly AppDbContext _db;

    public DeleteParentChildCommandHandler(AppDbContext db)
    {
        _db = db;
    }

    public async Task<DeleteParentChildResult> Handle(DeleteParentChildCommand command, CancellationToken cancellationToken)
    {
        var scopedParentIds = await ApiEndpointHelpers.ResolveParentScopeIdsAsync(_db, command.ParentId);

        var child = await _db.Children.FirstOrDefaultAsync(
            x => x.Id == command.ChildId && scopedParentIds.Contains(x.ParentId),
            cancellationToken);

        if (child is null)
        {
            return DeleteParentChildResult.NotFound();
        }

        AppUser? childUser = null;
        if (child.UserId.HasValue)
        {
            childUser = await _db.Users.FirstOrDefaultAsync(
                x => x.Id == child.UserId.Value && x.Role == UserRole.Child,
                cancellationToken);
        }

        _db.Children.Remove(child);
        if (childUser is not null)
        {
            _db.Users.Remove(childUser);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return DeleteParentChildResult.NoContent();
    }
}