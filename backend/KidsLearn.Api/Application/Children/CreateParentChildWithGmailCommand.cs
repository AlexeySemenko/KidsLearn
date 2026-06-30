using MediatR;
using Microsoft.EntityFrameworkCore;

public sealed record CreateParentChildWithGmailCommand(Guid ParentId, CreateChildWithEmailRequest Request) : IRequest<CreateParentChildWithGmailResult>;

public sealed record CreateParentChildWithGmailResult(CreatedChildWithEmailResponse? Response, string? Error, int StatusCode)
{
    public static CreateParentChildWithGmailResult BadRequest(string error)
        => new(null, error, StatusCodes.Status400BadRequest);

    public static CreateParentChildWithGmailResult NotFound(string error)
        => new(null, error, StatusCodes.Status404NotFound);

    public static CreateParentChildWithGmailResult Created(CreatedChildWithEmailResponse response)
        => new(response, null, StatusCodes.Status201Created);
}

public sealed class CreateParentChildWithGmailCommandHandler : IRequestHandler<CreateParentChildWithGmailCommand, CreateParentChildWithGmailResult>
{
    private readonly AppDbContext _db;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;

    public CreateParentChildWithGmailCommandHandler(AppDbContext db, IEmailService emailService, IConfiguration configuration)
    {
        _db = db;
        _emailService = emailService;
        _configuration = configuration;
    }

    public async Task<CreateParentChildWithGmailResult> Handle(CreateParentChildWithGmailCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;
        var scopedParentIds = await ApiEndpointHelpers.ResolveParentScopeIdsAsync(_db, command.ParentId);

        if (string.IsNullOrWhiteSpace(request.Name))
            return CreateParentChildWithGmailResult.BadRequest("Name is required.");

        if (!ApiEndpointHelpers.IsGradeInRange(request.Grade))
            return CreateParentChildWithGmailResult.BadRequest("Grade must be between 1 and 12.");

        if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
            return CreateParentChildWithGmailResult.BadRequest("A valid email address is required.");

        var email = request.Email.Trim().ToLowerInvariant();

        var parentUser = await _db.Users.FirstOrDefaultAsync(
            x => x.Id == command.ParentId && (x.Role == UserRole.Parent || x.Role == UserRole.Admin),
            cancellationToken);

        if (parentUser is null)
            return CreateParentChildWithGmailResult.NotFound("Parent was not found.");

        if (parentUser.Email.Equals(email, StringComparison.OrdinalIgnoreCase))
            return CreateParentChildWithGmailResult.BadRequest("Cannot add your own email as a child.");

        // Check for an existing child record with this enrollment email
        var existingChild = await _db.Children
            .FirstOrDefaultAsync(x => x.EnrollmentEmail == email, cancellationToken);

        if (existingChild is not null)
        {
            if (scopedParentIds.Contains(existingChild.ParentId))
            {
                // Already enrolled under this parent — idempotent
                return CreateParentChildWithGmailResult.Created(new CreatedChildWithEmailResponse(
                    new ChildResponse(existingChild.Id, existingChild.ParentId, existingChild.Name, existingChild.Grade, email, existingChild.UserId is null)));
            }
            return CreateParentChildWithGmailResult.BadRequest("This email is already linked to another child.");
        }

        // Also guard against a registered AppUser with that email (e.g. a parent account)
        var existingUser = await _db.Users.FirstOrDefaultAsync(x => x.Email == email, cancellationToken);
        if (existingUser is not null && existingUser.Role != UserRole.Child)
            return CreateParentChildWithGmailResult.BadRequest("This email is already used by a non-child account.");

        var registrationToken = Guid.NewGuid().ToString();
        var child = new Child
        {
            ParentId = command.ParentId,
            UserId = null,
            Name = request.Name.Trim(),
            Grade = request.Grade,
            EnrollmentEmail = email,
            RegistrationToken = registrationToken,
        };

        _db.Children.Add(child);
        await _db.SaveChangesAsync(cancellationToken);

        var childName = child.Name;
        var childGrade = child.Grade;
        var parentEmail = parentUser.Email;
        var parentName = parentUser.DisplayName ?? parentUser.Email;
        var emailService = _emailService;
        var frontendBase = _configuration["FrontendBaseUrl"]?.TrimEnd('/') ?? "http://localhost:8080";
        var registerUrl = $"{frontendBase}/register/child?token={Uri.EscapeDataString(registrationToken)}";

        _ = Task.Run(async () =>
        {
            try { await emailService.SendChildAddedToParentAsync(parentEmail, parentName, childName, childGrade); } catch { }
            try { await emailService.SendChildWelcomeAsync(email, childName, parentEmail, registerUrl); } catch { }
        });

        return CreateParentChildWithGmailResult.Created(new CreatedChildWithEmailResponse(
            new ChildResponse(child.Id, child.ParentId, child.Name, child.Grade, email, IsPending: true)));
    }
}
