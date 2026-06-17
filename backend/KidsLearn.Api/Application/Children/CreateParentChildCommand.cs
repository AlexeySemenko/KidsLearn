using MediatR;
using Microsoft.EntityFrameworkCore;

public sealed record CreateParentChildCommand(Guid ParentId, CreateChildRequest Request) : IRequest<CreateParentChildResult>;

public sealed record CreateParentChildResult(CreatedChildResponse? Response, string? Error, int StatusCode)
{
    public static CreateParentChildResult BadRequest(string error)
        => new(null, error, StatusCodes.Status400BadRequest);

    public static CreateParentChildResult NotFound(string error)
        => new(null, error, StatusCodes.Status404NotFound);

    public static CreateParentChildResult Created(CreatedChildResponse response)
        => new(response, null, StatusCodes.Status201Created);
}

public sealed class CreateParentChildCommandHandler : IRequestHandler<CreateParentChildCommand, CreateParentChildResult>
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasherService _passwordHasher;

    public CreateParentChildCommandHandler(AppDbContext db, IPasswordHasherService passwordHasher)
    {
        _db = db;
        _passwordHasher = passwordHasher;
    }

    public async Task<CreateParentChildResult> Handle(CreateParentChildCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return CreateParentChildResult.BadRequest("Name is required.");
        }

        if (!ApiEndpointHelpers.IsGradeInRange(request.Grade))
        {
            return CreateParentChildResult.BadRequest("Grade must be between 1 and 12.");
        }

        var parentExists = await _db.Users.AnyAsync(
            x => x.Id == command.ParentId && (x.Role == UserRole.Parent || x.Role == UserRole.Admin),
            cancellationToken);

        if (!parentExists)
        {
            return CreateParentChildResult.NotFound("Parent was not found.");
        }

        var accessCode = string.IsNullOrWhiteSpace(request.AccessCode)
            ? ApiEndpointHelpers.GenerateAccessCode()
            : request.AccessCode.Trim();

        if (accessCode.Length < 4)
        {
            return CreateParentChildResult.BadRequest("Access code must contain at least 4 characters.");
        }

        var childUser = new AppUser
        {
            Email = $"child-{Guid.NewGuid():N}@kidslearn.local",
            PasswordHash = _passwordHasher.HashPassword(accessCode),
            Role = UserRole.Child,
            CreatedAt = DateTime.UtcNow
        };

        var child = new Child
        {
            ParentId = command.ParentId,
            User = childUser,
            Name = request.Name.Trim(),
            Grade = request.Grade
        };

        _db.Children.Add(child);
        await _db.SaveChangesAsync(cancellationToken);

        var response = new CreatedChildResponse(
            new ChildResponse(child.Id, child.ParentId, child.Name, child.Grade, null),
            accessCode);

        return CreateParentChildResult.Created(response);
    }
}