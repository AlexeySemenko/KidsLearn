using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Xunit;

public class AdminCommandHandlersUnitTests
{
    // ── GetAdminUsersQuery ──────────────────────────────────────────────────

    [Fact]
    public async Task GetAdminUsersQuery_ReturnsAllUsers_OrderedByCreatedAt()
    {
        await using var db = CreateDbContext();

        var older = CreateUser("a@example.com", UserRole.Parent, createdAt: DateTime.UtcNow.AddDays(-1));
        var newer = CreateUser("b@example.com", UserRole.Child,  createdAt: DateTime.UtcNow);
        db.Users.AddRange(older, newer);
        await db.SaveChangesAsync();

        var handler = new GetAdminUsersQueryHandler(db);
        var result = await handler.Handle(new GetAdminUsersQuery(), CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal("a@example.com", result[0].Email);
        Assert.Equal("b@example.com", result[1].Email);
    }

    [Fact]
    public async Task GetAdminUsersQuery_IncludesAllRoles()
    {
        await using var db = CreateDbContext();
        db.Users.AddRange(
            CreateUser("admin@example.com",  UserRole.Admin),
            CreateUser("parent@example.com", UserRole.Parent),
            CreateUser("child@example.com",  UserRole.Child));
        await db.SaveChangesAsync();

        var handler = new GetAdminUsersQueryHandler(db);
        var result = await handler.Handle(new GetAdminUsersQuery(), CancellationToken.None);

        Assert.Equal(3, result.Count);
        Assert.Contains(result, u => u.Role == "Admin");
        Assert.Contains(result, u => u.Role == "Parent");
        Assert.Contains(result, u => u.Role == "Child");
    }

    // ── CreateAdminUserCommand ──────────────────────────────────────────────

    [Fact]
    public async Task CreateAdminUserCommand_CreatesUser_AndReturnsEmailSentTrue()
    {
        await using var db = CreateDbContext();
        var email = new FakeEmailService(invitationResult: true);

        var handler = new CreateAdminUserCommandHandler(db, email);
        var result = await handler.Handle(
            new CreateAdminUserCommand(
                new AdminCreateUserRequest("new@example.com", "Alice", "Parent"),
                "admin@example.com"),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        Assert.NotNull(result.User);
        Assert.Equal("new@example.com", result.User!.Email);
        Assert.Equal("Alice", result.User.DisplayName);
        Assert.Equal("Parent", result.User.Role);
        Assert.True(result.EmailSent);
        Assert.Equal(1, await db.Users.CountAsync());
    }

    [Fact]
    public async Task CreateAdminUserCommand_ReturnsEmailSentFalse_WhenEmailServiceFails()
    {
        await using var db = CreateDbContext();
        var email = new FakeEmailService(invitationResult: false);

        var handler = new CreateAdminUserCommandHandler(db, email);
        var result = await handler.Handle(
            new CreateAdminUserCommand(
                new AdminCreateUserRequest("new@example.com", null, "Child"),
                "admin@example.com"),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        Assert.False(result.EmailSent);
    }

    [Fact]
    public async Task CreateAdminUserCommand_ReturnsConflict_WhenEmailAlreadyExists()
    {
        await using var db = CreateDbContext();
        db.Users.Add(CreateUser("exists@example.com", UserRole.Parent));
        await db.SaveChangesAsync();

        var handler = new CreateAdminUserCommandHandler(db, new FakeEmailService());
        var result = await handler.Handle(
            new CreateAdminUserCommand(
                new AdminCreateUserRequest("exists@example.com", null, "Parent"),
                "admin@example.com"),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Contains("already exists", result.Error);
    }

    [Fact]
    public async Task CreateAdminUserCommand_ReturnsBadRequest_WhenRoleInvalid()
    {
        await using var db = CreateDbContext();

        var handler = new CreateAdminUserCommandHandler(db, new FakeEmailService());
        var result = await handler.Handle(
            new CreateAdminUserCommand(
                new AdminCreateUserRequest("new@example.com", null, "Wizard"),
                "admin@example.com"),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Contains("Invalid role", result.Error);
    }

    [Fact]
    public async Task CreateAdminUserCommand_ReturnsBadRequest_WhenEmailEmpty()
    {
        await using var db = CreateDbContext();

        var handler = new CreateAdminUserCommandHandler(db, new FakeEmailService());
        var result = await handler.Handle(
            new CreateAdminUserCommand(
                new AdminCreateUserRequest("", null, "Parent"),
                "admin@example.com"),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
    }

    // ── UpdateAdminUserCommand ──────────────────────────────────────────────

    [Fact]
    public async Task UpdateAdminUserCommand_UpdatesDisplayNameAndRole()
    {
        await using var db = CreateDbContext();
        var user = CreateUser("user@example.com", UserRole.Child);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var handler = new UpdateAdminUserCommandHandler(db);
        var result = await handler.Handle(
            new UpdateAdminUserCommand(user.Id, new AdminUpdateUserRequest("Bob", "Parent")),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal("Bob", result.User!.DisplayName);
        Assert.Equal("Parent", result.User.Role);

        var updated = await db.Users.FindAsync(user.Id);
        Assert.Equal("Bob", updated!.DisplayName);
        Assert.Equal(UserRole.Parent, updated.Role);
    }

    [Fact]
    public async Task UpdateAdminUserCommand_UpdatesOnlyDisplayName_WhenRoleOmitted()
    {
        await using var db = CreateDbContext();
        var user = CreateUser("user@example.com", UserRole.Child);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var handler = new UpdateAdminUserCommandHandler(db);
        var result = await handler.Handle(
            new UpdateAdminUserCommand(user.Id, new AdminUpdateUserRequest("Charlie", null)),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal("Charlie", result.User!.DisplayName);
        Assert.Equal("Child", result.User.Role);  // unchanged
    }

    [Fact]
    public async Task UpdateAdminUserCommand_ReturnsNotFound_WhenUserMissing()
    {
        await using var db = CreateDbContext();

        var handler = new UpdateAdminUserCommandHandler(db);
        var result = await handler.Handle(
            new UpdateAdminUserCommand(Guid.NewGuid(), new AdminUpdateUserRequest("X", "Parent")),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
    }

    [Fact]
    public async Task UpdateAdminUserCommand_ReturnsBadRequest_WhenRoleInvalid()
    {
        await using var db = CreateDbContext();
        var user = CreateUser("user@example.com", UserRole.Parent);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var handler = new UpdateAdminUserCommandHandler(db);
        var result = await handler.Handle(
            new UpdateAdminUserCommand(user.Id, new AdminUpdateUserRequest(null, "Wizard")),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
    }

    // ── DeleteAdminUserCommand ──────────────────────────────────────────────

    [Fact]
    public async Task DeleteAdminUserCommand_DeletesUser_WhenNotSelf()
    {
        await using var db = CreateDbContext();
        var caller = CreateUser("admin@example.com", UserRole.Admin);
        var target = CreateUser("victim@example.com", UserRole.Parent);
        db.Users.AddRange(caller, target);
        await db.SaveChangesAsync();

        var handler = new DeleteAdminUserCommandHandler(db);
        var result = await handler.Handle(
            new DeleteAdminUserCommand(target.Id, caller.Id),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status204NoContent, result.StatusCode);
        Assert.False(await db.Users.AnyAsync(u => u.Id == target.Id));
    }

    [Fact]
    public async Task DeleteAdminUserCommand_ReturnsBadRequest_WhenDeletingSelf()
    {
        await using var db = CreateDbContext();
        var admin = CreateUser("admin@example.com", UserRole.Admin);
        db.Users.Add(admin);
        await db.SaveChangesAsync();

        var handler = new DeleteAdminUserCommandHandler(db);
        var result = await handler.Handle(
            new DeleteAdminUserCommand(admin.Id, admin.Id),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.True(await db.Users.AnyAsync(u => u.Id == admin.Id));
    }

    [Fact]
    public async Task DeleteAdminUserCommand_ReturnsNotFound_WhenUserMissing()
    {
        await using var db = CreateDbContext();

        var handler = new DeleteAdminUserCommandHandler(db);
        var result = await handler.Handle(
            new DeleteAdminUserCommand(Guid.NewGuid(), Guid.NewGuid()),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"admin-handler-tests-{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    private static AppUser CreateUser(string email, UserRole role, string? passwordHash = null, DateTime? createdAt = null)
    {
        return new AppUser
        {
            Email = email,
            PasswordHash = passwordHash ?? "hash",
            Role = role,
            CreatedAt = createdAt ?? DateTime.UtcNow,
        };
    }

    private sealed class FakeEmailService(bool invitationResult = true) : IEmailService
    {
        public Task<bool> SendInvitationAsync(string toEmail, string? displayName, string inviterName)
            => Task.FromResult(invitationResult);

        public Task<bool> SendParentLinkedAsync(string toEmail, string? displayName, string linkedByEmail)
            => Task.FromResult(true);
    }
}
