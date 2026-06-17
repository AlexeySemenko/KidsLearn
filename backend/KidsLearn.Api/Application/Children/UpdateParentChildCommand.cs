using MediatR;
using Microsoft.EntityFrameworkCore;

public sealed record UpdateParentChildCommand(Guid ParentId, Guid ChildId, UpdateChildRequest Request) : IRequest<UpdateParentChildResult>;

public sealed record UpdateParentChildResult(ChildResponse? Response, string? Error, int StatusCode)
{
    public static UpdateParentChildResult BadRequest(string error)
        => new(null, error, StatusCodes.Status400BadRequest);

    public static UpdateParentChildResult NotFound()
        => new(null, null, StatusCodes.Status404NotFound);

    public static UpdateParentChildResult Success(ChildResponse response)
        => new(response, null, StatusCodes.Status200OK);
}

public sealed class UpdateParentChildCommandHandler : IRequestHandler<UpdateParentChildCommand, UpdateParentChildResult>
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasherService _passwordHasher;

    public UpdateParentChildCommandHandler(AppDbContext db, IPasswordHasherService passwordHasher)
    {
        _db = db;
        _passwordHasher = passwordHasher;
    }

    public async Task<UpdateParentChildResult> Handle(UpdateParentChildCommand command, CancellationToken cancellationToken)
    {
        var scopedParentIds = await ApiEndpointHelpers.ResolveParentScopeIdsAsync(_db, command.ParentId);

        var child = await _db.Children
            .Include(x => x.User)
            .FirstOrDefaultAsync(
                x => x.Id == command.ChildId && scopedParentIds.Contains(x.ParentId),
                cancellationToken);

        if (child is null)
        {
            return UpdateParentChildResult.NotFound();
        }

        var request = command.Request;

        if (request.Name is not null && string.IsNullOrWhiteSpace(request.Name))
        {
            return UpdateParentChildResult.BadRequest("Name cannot be empty.");
        }

        if (request.Name is not null)
        {
            child.Name = request.Name.Trim();
        }

        if (request.Grade.HasValue)
        {
            if (!ApiEndpointHelpers.IsGradeInRange(request.Grade.Value))
            {
                return UpdateParentChildResult.BadRequest("Grade must be between 1 and 12.");
            }

            child.Grade = request.Grade.Value;
        }

        if (request.AccessCode is not null)
        {
            if (string.IsNullOrWhiteSpace(request.AccessCode) || request.AccessCode.Trim().Length < 4)
            {
                return UpdateParentChildResult.BadRequest("Access code must contain at least 4 characters.");
            }

            if (child.UserId.HasValue)
            {
                var childUser = await _db.Users.FirstOrDefaultAsync(
                    x => x.Id == child.UserId.Value && x.Role == UserRole.Child,
                    cancellationToken);

                if (childUser is not null)
                {
                    childUser.PasswordHash = _passwordHasher.HashPassword(request.AccessCode.Trim());
                }
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        return UpdateParentChildResult.Success(new ChildResponse(child.Id, child.ParentId, child.Name, child.Grade, child.User?.Email));
    }
}