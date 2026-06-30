using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using Xunit;

public class AuthCommandHandlersUnitTests
{
    [Fact]
    public async Task RegisterParentCommand_CreatesUser_WhenInputIsValid()
    {
        await using var db = CreateDbContext();
        var handler = new RegisterParentCommandHandler(db, new PasswordHasherService(), new NullEmailService(), CreateAuthConfiguration());

        var result = await handler.Handle(
            new RegisterParentCommand(new RegisterRequest("new.parent@example.com", "Parent123!")),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        Assert.NotNull(result.User);

        var saved = await db.Users.SingleOrDefaultAsync(x => x.Email == "new.parent@example.com");
        Assert.NotNull(saved);
        Assert.Equal(UserRole.Parent, saved.Role);
    }

    [Fact]
    public async Task RegisterParentCommand_ReturnsBadRequest_WhenEmailMissing()
    {
        await using var db = CreateDbContext();
        var handler = new RegisterParentCommandHandler(db, new PasswordHasherService(), new NullEmailService(), CreateAuthConfiguration());

        var result = await handler.Handle(
            new RegisterParentCommand(new RegisterRequest("", "Password1!")),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
    }

    [Fact]
    public async Task RegisterParentCommand_ReturnsBadRequest_WhenEmailInvalid()
    {
        await using var db = CreateDbContext();
        var handler = new RegisterParentCommandHandler(db, new PasswordHasherService(), new NullEmailService(), CreateAuthConfiguration());

        var result = await handler.Handle(
            new RegisterParentCommand(new RegisterRequest("notanemail", "Password1!")),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
    }

    [Fact]
    public async Task RegisterParentCommand_ReturnsBadRequest_WhenPasswordTooShort()
    {
        await using var db = CreateDbContext();
        var handler = new RegisterParentCommandHandler(db, new PasswordHasherService(), new NullEmailService(), CreateAuthConfiguration());

        var result = await handler.Handle(
            new RegisterParentCommand(new RegisterRequest("user@example.com", "short")),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Contains("8 characters", result.Error);
    }

    [Fact]
    public async Task RegisterParentCommand_ReturnsConflict_WhenEmailAlreadyExists()
    {
        await using var db = CreateDbContext();
        db.Users.Add(new AppUser { Email = "existing@example.com", PasswordHash = "hash", Role = UserRole.Parent, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var handler = new RegisterParentCommandHandler(db, new PasswordHasherService(), new NullEmailService(), CreateAuthConfiguration());

        var result = await handler.Handle(
            new RegisterParentCommand(new RegisterRequest("existing@example.com", "Password1!")),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
    }

    [Fact]
    public async Task RegisterParentCommand_ReturnsBadRequest_WhenEmailNotInAllowedList()
    {
        await using var db = CreateDbContext();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AllowedRegistrationEmails:0"] = "allowed@example.com",
                ["Jwt:RefreshTokenExpirationDays"] = "14",
                ["Jwt:AccessTokenExpirationMinutes"] = "30"
            })
            .Build();

        var handler = new RegisterParentCommandHandler(db, new PasswordHasherService(), new NullEmailService(), config);

        var result = await handler.Handle(
            new RegisterParentCommand(new RegisterRequest("other@example.com", "Password1!")),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Contains("not permitted", result.Error);
    }

    [Fact]
    public async Task RegisterParentCommand_Succeeds_WhenEmailIsInAllowedList()
    {
        await using var db = CreateDbContext();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AllowedRegistrationEmails:0"] = "allowed@example.com",
                ["Jwt:RefreshTokenExpirationDays"] = "14",
                ["Jwt:AccessTokenExpirationMinutes"] = "30"
            })
            .Build();

        var handler = new RegisterParentCommandHandler(db, new PasswordHasherService(), new NullEmailService(), config);

        var result = await handler.Handle(
            new RegisterParentCommand(new RegisterRequest("allowed@example.com", "Password1!")),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
    }

    [Fact]
    public async Task RegisterParentCommand_HashesPassword_NotStoredAsPlaintext()
    {
        await using var db = CreateDbContext();
        var handler = new RegisterParentCommandHandler(db, new PasswordHasherService(), new NullEmailService(), CreateAuthConfiguration());

        await handler.Handle(
            new RegisterParentCommand(new RegisterRequest("user@example.com", "MySecret1!")),
            CancellationToken.None);

        var saved = await db.Users.SingleAsync(x => x.Email == "user@example.com");
        Assert.NotEqual("MySecret1!", saved.PasswordHash);
        Assert.True(new PasswordHasherService().VerifyPassword("MySecret1!", saved.PasswordHash));
    }

    // ── RegisterChildCommand ──────────────────────────────────────────────────

    [Fact]
    public async Task RegisterChildCommand_CreatesUserAndLinksChild_WhenEnrolled()
    {
        await using var db = CreateDbContext();
        var token = Guid.NewGuid().ToString();
        var parent = new AppUser { Email = "parent@example.com", PasswordHash = "hash", Role = UserRole.Parent, CreatedAt = DateTime.UtcNow };
        var child = new Child { ParentId = parent.Id, Parent = parent, Name = "Mia", Grade = 4, EnrollmentEmail = "mia@example.com", RegistrationToken = token };
        db.Users.Add(parent);
        db.Children.Add(child);
        await db.SaveChangesAsync();

        var handler = new RegisterChildCommandHandler(db, new PasswordHasherService(), new FakeJwtTokenService(), CreateAuthConfiguration());

        var result = await handler.Handle(
            new RegisterChildCommand(new RegisterChildRequest(token, "Password1!")),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        Assert.NotNull(result.Response);
        Assert.Equal("Child", result.Response!.User.Role);
        Assert.Equal("Mia", result.Response.User.DisplayName);

        var updatedChild = await db.Children.Include(x => x.User).SingleAsync(x => x.Id == child.Id);
        Assert.NotNull(updatedChild.UserId);
        Assert.Equal("mia@example.com", updatedChild.User!.Email);
        Assert.Equal(1, await db.RefreshTokens.CountAsync(x => x.UserId == updatedChild.UserId));
    }

    [Fact]
    public async Task RegisterChildCommand_ReturnsBadRequest_WhenTokenInvalid()
    {
        await using var db = CreateDbContext();
        var handler = new RegisterChildCommandHandler(db, new PasswordHasherService(), new FakeJwtTokenService(), CreateAuthConfiguration());

        var result = await handler.Handle(
            new RegisterChildCommand(new RegisterChildRequest(Guid.NewGuid().ToString(), "Password1!")),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Contains("invalid", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RegisterChildCommand_ReturnsConflict_WhenAlreadyRegistered()
    {
        await using var db = CreateDbContext();
        var token = Guid.NewGuid().ToString();
        var parent = new AppUser { Email = "parent@example.com", PasswordHash = "hash", Role = UserRole.Parent, CreatedAt = DateTime.UtcNow };
        var childUser = new AppUser { Email = "mia@example.com", PasswordHash = "existing-hash", Role = UserRole.Child, CreatedAt = DateTime.UtcNow };
        var child = new Child { ParentId = parent.Id, Parent = parent, User = childUser, UserId = childUser.Id, Name = "Mia", Grade = 4, EnrollmentEmail = "mia@example.com", RegistrationToken = token };
        db.Users.AddRange(parent, childUser);
        db.Children.Add(child);
        await db.SaveChangesAsync();

        var handler = new RegisterChildCommandHandler(db, new PasswordHasherService(), new FakeJwtTokenService(), CreateAuthConfiguration());

        var result = await handler.Handle(
            new RegisterChildCommand(new RegisterChildRequest(token, "Password1!")),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Contains("already set up", result.Error);
    }

    [Fact]
    public async Task RegisterChildCommand_ReturnsBadRequest_WhenPasswordTooShort()
    {
        await using var db = CreateDbContext();
        var token = Guid.NewGuid().ToString();
        var parent = new AppUser { Email = "parent@example.com", PasswordHash = "hash", Role = UserRole.Parent, CreatedAt = DateTime.UtcNow };
        var child = new Child { ParentId = parent.Id, Parent = parent, Name = "Mia", Grade = 4, EnrollmentEmail = "mia@example.com", RegistrationToken = token };
        db.Users.Add(parent);
        db.Children.Add(child);
        await db.SaveChangesAsync();

        var handler = new RegisterChildCommandHandler(db, new PasswordHasherService(), new FakeJwtTokenService(), CreateAuthConfiguration());

        var result = await handler.Handle(
            new RegisterChildCommand(new RegisterChildRequest(token, "short")),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Contains("8 characters", result.Error);
    }

    [Fact]
    public async Task LoginParentCommand_ReturnsUnauthorized_ForInvalidPassword()
    {
        await using var db = CreateDbContext();
        var hasher = new PasswordHasherService();
        db.Users.Add(new AppUser
        {
            Email = "parent@example.com",
            PasswordHash = hasher.HashPassword("CorrectPass1!"),
            Role = UserRole.Parent,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var handler = new LoginParentCommandHandler(
            db,
            hasher,
            new FakeJwtTokenService(),
            CreateAuthConfiguration());

        var result = await handler.Handle(
            new LoginParentCommand(new LoginRequest("parent@example.com", "WrongPass1!")),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
        Assert.Null(result.Response);
    }

    [Fact]
    public async Task LoginChildCommand_IssuesTokens_AndStoresRefreshToken()
    {
        await using var db = CreateDbContext();
        var hasher = new PasswordHasherService();

        var parent = new AppUser
        {
            Email = "parent@example.com",
            PasswordHash = hasher.HashPassword("Parent123!"),
            Role = UserRole.Parent,
            CreatedAt = DateTime.UtcNow
        };
        var childUser = new AppUser
        {
            Email = "child@example.com",
            PasswordHash = hasher.HashPassword("2468"),
            Role = UserRole.Child,
            CreatedAt = DateTime.UtcNow
        };
        var child = new Child
        {
            ParentId = parent.Id,
            UserId = childUser.Id,
            Parent = parent,
            User = childUser,
            Name = "Kid",
            Grade = 3
        };

        db.Users.AddRange(parent, childUser);
        db.Children.Add(child);
        await db.SaveChangesAsync();

        var handler = new LoginChildCommandHandler(
            db,
            hasher,
            new FakeJwtTokenService(),
            CreateAuthConfiguration());

        var result = await handler.Handle(
            new LoginChildCommand(new ChildLoginRequest(child.Id, "2468")),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.NotNull(result.Response);
        Assert.Equal(childUser.Id, result.Response.User.Id);
        Assert.Equal(1, await db.RefreshTokens.CountAsync(x => x.UserId == childUser.Id));
    }

    [Fact]
    public async Task RefreshAuthTokenCommand_RotatesRefreshToken_WhenTokenIsValid()
    {
        await using var db = CreateDbContext();
        var user = new AppUser
        {
            Email = "parent@example.com",
            PasswordHash = "hash",
            Role = UserRole.Parent,
            CreatedAt = DateTime.UtcNow
        };

        var oldToken = new RefreshToken
        {
            UserId = user.Id,
            User = user,
            Token = "old-token",
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            ExpiresAt = DateTime.UtcNow.AddDays(10)
        };

        db.Users.Add(user);
        db.RefreshTokens.Add(oldToken);
        await db.SaveChangesAsync();

        var handler = new RefreshAuthTokenCommandHandler(
            db,
            new FakeJwtTokenService(),
            CreateAuthConfiguration());

        var result = await handler.Handle(
            new RefreshAuthTokenCommand(new RefreshRequest("old-token")),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.NotNull(result.Response);

        var persistedOld = await db.RefreshTokens.SingleAsync(x => x.Token == "old-token");
        Assert.NotNull(persistedOld.RevokedAt);
        Assert.Equal(2, await db.RefreshTokens.CountAsync(x => x.UserId == user.Id));
    }

    [Fact]
    public async Task RevokeAuthTokenCommand_ReturnsNotFound_WhenTokenMissing()
    {
        await using var db = CreateDbContext();
        var handler = new RevokeAuthTokenCommandHandler(db);

        var result = await handler.Handle(
            new RevokeAuthTokenCommand(new RevokeRequest("missing-token")),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"auth-handler-tests-{Guid.NewGuid():N}")
            .Options;

        return new AppDbContext(options);
    }

    private static IConfiguration CreateAuthConfiguration()
    {
        var settings = new Dictionary<string, string?>
        {
            ["Jwt:RefreshTokenExpirationDays"] = "14",
            ["Jwt:AccessTokenExpirationMinutes"] = "30"
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();
    }

    private sealed class NullEmailService : IEmailService
    {
        public Task<bool> SendInvitationAsync(string toEmail, string? displayName, string inviterName) => Task.FromResult(true);
        public Task<bool> SendParentLinkedAsync(string toEmail, string? displayName, string linkedByEmail) => Task.FromResult(true);
        public Task<bool> SendFriendInviteAsync(string toEmail, string inviterName, string inviteUrl) => Task.FromResult(true);
        public Task<bool> SendAssignmentCompletedToParentAsync(string toEmail, string parentName, string childName, string lessonTitle, decimal score, int correctAnswers, int totalQuestions, IList<(string LessonTitle, decimal Score)> recentResults) => Task.FromResult(true);
        public Task<bool> SendAssignmentCreatedToChildAsync(string toEmail, string childName, string lessonTitle, string subject, DateTime? dueDate) => Task.FromResult(true);
        public Task<bool> SendWelcomeToParentAsync(string toEmail, string? displayName) => Task.FromResult(true);
        public Task<bool> SendChildAddedToParentAsync(string toEmail, string? parentName, string childName, int grade) => Task.FromResult(true);
        public Task<bool> SendChildWelcomeAsync(string toEmail, string childName, string parentEmail, string registerUrl) => Task.FromResult(true);
    }

    private sealed class FakeJwtTokenService : IJwtTokenService
    {
        private int _refreshCounter;

        public string CreateAccessToken(AppUser user)
        {
            return $"access-{user.Id}";
        }

        public string CreateRefreshToken()
        {
            _refreshCounter++;
            return $"refresh-{_refreshCounter}";
        }
    }
}