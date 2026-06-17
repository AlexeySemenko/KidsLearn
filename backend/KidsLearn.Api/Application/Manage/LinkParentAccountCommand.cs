using MediatR;
using Microsoft.EntityFrameworkCore;

public sealed record LinkParentAccountCommand(Guid ParentId, string Email) : IRequest<LinkParentAccountResult>;

public sealed record LinkParentAccountResult(LinkedParentResponse? Response, bool EmailSent, string? Error, int StatusCode)
{
    public static LinkParentAccountResult BadRequest(string error)
        => new(null, false, error, StatusCodes.Status400BadRequest);

    public static LinkParentAccountResult NotFound(string error)
        => new(null, false, error, StatusCodes.Status404NotFound);

    public static LinkParentAccountResult Created(LinkedParentResponse response, bool emailSent)
        => new(response, emailSent, null, StatusCodes.Status201Created);
}

public sealed class LinkParentAccountCommandHandler : IRequestHandler<LinkParentAccountCommand, LinkParentAccountResult>
{
    private readonly AppDbContext _db;
    private readonly IEmailService _emailService;

    public LinkParentAccountCommandHandler(AppDbContext db, IEmailService emailService)
    {
        _db = db;
        _emailService = emailService;
    }

    public async Task<LinkParentAccountResult> Handle(LinkParentAccountCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Email))
            return LinkParentAccountResult.BadRequest("Parent email is required.");

        var normalizedEmail = command.Email.Trim().ToLowerInvariant();

        var parentUser = await _db.Users
            .FirstOrDefaultAsync(x => x.Id == command.ParentId &&
                (x.Role == UserRole.Parent || x.Role == UserRole.Admin), cancellationToken);

        if (parentUser is null)
            return LinkParentAccountResult.NotFound("Current parent account was not found.");

        if (string.Equals(parentUser.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase))
            return LinkParentAccountResult.BadRequest("You cannot link your own parent account.");

        var otherParent = await _db.Users
            .FirstOrDefaultAsync(x => x.Email == normalizedEmail && x.Role == UserRole.Parent, cancellationToken);

        if (otherParent is null)
            return LinkParentAccountResult.NotFound("Parent with this email was not found.");

        var (parentAId, parentBId) = ApiEndpointHelpers.NormalizeParentLinkPair(command.ParentId, otherParent.Id);
        var exists = await _db.ParentAccountLinks
            .AnyAsync(x => x.ParentAId == parentAId && x.ParentBId == parentBId, cancellationToken);

        if (exists)
            return LinkParentAccountResult.BadRequest("These parent accounts are already linked.");

        var link = new ParentAccountLink
        {
            ParentAId = parentAId,
            ParentBId = parentBId,
            CreatedAt = DateTime.UtcNow
        };

        _db.ParentAccountLinks.Add(link);
        await _db.SaveChangesAsync(cancellationToken);

        var emailSent = await _emailService.SendParentLinkedAsync(otherParent.Email, otherParent.DisplayName, parentUser.Email);

        return LinkParentAccountResult.Created(new LinkedParentResponse(otherParent.Id, otherParent.Email, link.CreatedAt), emailSent);
    }
}
