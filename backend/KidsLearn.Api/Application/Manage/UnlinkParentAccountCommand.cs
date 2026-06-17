using MediatR;
using Microsoft.EntityFrameworkCore;

public sealed record UnlinkParentAccountCommand(Guid ParentId, Guid LinkedParentId) : IRequest<UnlinkParentAccountResult>;

public sealed record UnlinkParentAccountResult(string? Error, int StatusCode)
{
    public static UnlinkParentAccountResult BadRequest(string error)
        => new(error, StatusCodes.Status400BadRequest);

    public static UnlinkParentAccountResult NotFound(string error)
        => new(error, StatusCodes.Status404NotFound);

    public static UnlinkParentAccountResult NoContent()
        => new(null, StatusCodes.Status204NoContent);
}

public sealed class UnlinkParentAccountCommandHandler : IRequestHandler<UnlinkParentAccountCommand, UnlinkParentAccountResult>
{
    private readonly AppDbContext _db;

    public UnlinkParentAccountCommandHandler(AppDbContext db)
    {
        _db = db;
    }

    public async Task<UnlinkParentAccountResult> Handle(UnlinkParentAccountCommand command, CancellationToken cancellationToken)
    {
        if (command.LinkedParentId == Guid.Empty || command.LinkedParentId == command.ParentId)
            return UnlinkParentAccountResult.BadRequest("Invalid linked parent id.");

        var (parentAId, parentBId) = ApiEndpointHelpers.NormalizeParentLinkPair(command.ParentId, command.LinkedParentId);

        var link = await _db.ParentAccountLinks
            .FirstOrDefaultAsync(x => x.ParentAId == parentAId && x.ParentBId == parentBId, cancellationToken);

        if (link is null)
            return UnlinkParentAccountResult.NotFound("Linked parent relation not found.");

        _db.ParentAccountLinks.Remove(link);
        await _db.SaveChangesAsync(cancellationToken);

        return UnlinkParentAccountResult.NoContent();
    }
}
