using MediatR;
using Microsoft.EntityFrameworkCore;

public sealed record CreateParentChildWithGmailCommand(Guid ParentId, CreateChildWithGmailRequest Request) : IRequest<CreateParentChildWithGmailResult>;

public sealed record CreateParentChildWithGmailResult(CreatedChildWithGmailResponse? Response, string? Error, int StatusCode)
{
    public static CreateParentChildWithGmailResult BadRequest(string error)
        => new(null, error, StatusCodes.Status400BadRequest);

    public static CreateParentChildWithGmailResult NotFound(string error)
        => new(null, error, StatusCodes.Status404NotFound);

    public static CreateParentChildWithGmailResult Created(CreatedChildWithGmailResponse response)
        => new(response, null, StatusCodes.Status201Created);
}

public sealed class CreateParentChildWithGmailCommandHandler : IRequestHandler<CreateParentChildWithGmailCommand, CreateParentChildWithGmailResult>
{
    private const string GoogleProviderName = "Google";
    private readonly AppDbContext _db;

    public CreateParentChildWithGmailCommandHandler(AppDbContext db)
    {
        _db = db;
    }

    public async Task<CreateParentChildWithGmailResult> Handle(CreateParentChildWithGmailCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return CreateParentChildWithGmailResult.BadRequest("Name is required.");
        }

        if (!ApiEndpointHelpers.IsGradeInRange(request.Grade))
        {
            return CreateParentChildWithGmailResult.BadRequest("Grade must be between 1 and 12.");
        }

        var gmailEmail = request.GmailEmail.Trim().ToLowerInvariant();
        if (!gmailEmail.EndsWith("@gmail.com", StringComparison.OrdinalIgnoreCase))
        {
            return CreateParentChildWithGmailResult.BadRequest("Email must be a valid Gmail address.");
        }

        var parentExists = await _db.Users.AnyAsync(
            x => x.Id == command.ParentId && x.Role == UserRole.Parent,
            cancellationToken);

        if (!parentExists)
        {
            return CreateParentChildWithGmailResult.NotFound("Parent was not found.");
        }

        var existingUser = await _db.Users.FirstOrDefaultAsync(
            x => x.Email == gmailEmail && x.ExternalProvider == GoogleProviderName,
            cancellationToken);

        if (existingUser is not null)
        {
            var existingChild = await _db.Children.FirstOrDefaultAsync(
                x => x.UserId == existingUser.Id,
                cancellationToken);

            if (existingChild is not null && existingChild.ParentId != command.ParentId)
            {
                return CreateParentChildWithGmailResult.BadRequest("This Gmail is already linked to another child.");
            }

            if (existingChild is not null && existingChild.ParentId == command.ParentId)
            {
                return CreateParentChildWithGmailResult.Created(new CreatedChildWithGmailResponse(
                    new ChildResponse(existingChild.Id, existingChild.ParentId, existingChild.Name, existingChild.Grade)));
            }
        }

        var childUser = existingUser ?? new AppUser
        {
            Email = gmailEmail,
            PasswordHash = string.Empty,
            Role = UserRole.Child,
            ExternalProvider = GoogleProviderName,
            ExternalSubject = null,
            EmailVerified = false,
            CreatedAt = DateTime.UtcNow
        };

        if (existingUser is null)
        {
            _db.Users.Add(childUser);
        }

        var child = new Child
        {
            ParentId = command.ParentId,
            User = childUser,
            Name = request.Name.Trim(),
            Grade = request.Grade
        };

        _db.Children.Add(child);
        await _db.SaveChangesAsync(cancellationToken);

        var response = new CreatedChildWithGmailResponse(
            new ChildResponse(child.Id, child.ParentId, child.Name, child.Grade));

        return CreateParentChildWithGmailResult.Created(response);
    }
}
