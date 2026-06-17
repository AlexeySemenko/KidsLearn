using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Xunit;

public class ManageCommandHandlersUnitTests
{
    // ── GetLinkedParentsQuery ───────────────────────────────────────────────

    [Fact]
    public async Task GetLinkedParentsQuery_ReturnsLinkedParents_ForGivenParent()
    {
        await using var db = CreateDbContext();

        var parentA = CreateUser("a@example.com", UserRole.Parent);
        var parentB = CreateUser("b@example.com", UserRole.Parent);
        var parentC = CreateUser("c@example.com", UserRole.Parent);
        db.Users.AddRange(parentA, parentB, parentC);

        var (aId, bId) = ApiEndpointHelpers.NormalizeParentLinkPair(parentA.Id, parentB.Id);
        db.ParentAccountLinks.Add(new ParentAccountLink { ParentAId = aId, ParentBId = bId, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var handler = new GetLinkedParentsQueryHandler(db);
        var result = await handler.Handle(new GetLinkedParentsQuery(parentA.Id), CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("b@example.com", result[0].Email);
    }

    [Fact]
    public async Task GetLinkedParentsQuery_ReturnsEmpty_WhenNoLinks()
    {
        await using var db = CreateDbContext();
        var parent = CreateUser("parent@example.com", UserRole.Parent);
        db.Users.Add(parent);
        await db.SaveChangesAsync();

        var handler = new GetLinkedParentsQueryHandler(db);
        var result = await handler.Handle(new GetLinkedParentsQuery(parent.Id), CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetLinkedParentsQuery_ReturnsLinked_WhenParentIsParentB()
    {
        await using var db = CreateDbContext();

        var parentA = CreateUser("a@example.com", UserRole.Parent);
        var parentB = CreateUser("b@example.com", UserRole.Parent);
        db.Users.AddRange(parentA, parentB);

        // Store with A < B (normalized) but query from B's perspective
        var (aId, bId) = ApiEndpointHelpers.NormalizeParentLinkPair(parentA.Id, parentB.Id);
        db.ParentAccountLinks.Add(new ParentAccountLink { ParentAId = aId, ParentBId = bId, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var handler = new GetLinkedParentsQueryHandler(db);
        var result = await handler.Handle(new GetLinkedParentsQuery(parentB.Id), CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("a@example.com", result[0].Email);
    }

    // ── LinkParentAccountCommand ────────────────────────────────────────────

    [Fact]
    public async Task LinkParentAccountCommand_CreatesLink_AndSendsEmail()
    {
        await using var db = CreateDbContext();
        var email = new FakeEmailService();

        var parentA = CreateUser("a@example.com", UserRole.Parent);
        var parentB = CreateUser("b@example.com", UserRole.Parent);
        db.Users.AddRange(parentA, parentB);
        await db.SaveChangesAsync();

        var handler = new LinkParentAccountCommandHandler(db, email);
        var result = await handler.Handle(
            new LinkParentAccountCommand(parentA.Id, "b@example.com"),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        Assert.NotNull(result.Response);
        Assert.Equal("b@example.com", result.Response!.Email);
        Assert.Equal(1, await db.ParentAccountLinks.CountAsync());
        Assert.True(email.ParentLinkedEmailSent);
    }

    [Fact]
    public async Task LinkParentAccountCommand_ReturnsNotFound_WhenTargetEmailNotParent()
    {
        await using var db = CreateDbContext();

        var parent = CreateUser("parent@example.com", UserRole.Parent);
        var child  = CreateUser("child@example.com",  UserRole.Child);
        db.Users.AddRange(parent, child);
        await db.SaveChangesAsync();

        var handler = new LinkParentAccountCommandHandler(db, new FakeEmailService());
        var result = await handler.Handle(
            new LinkParentAccountCommand(parent.Id, "child@example.com"),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal(0, await db.ParentAccountLinks.CountAsync());
    }

    [Fact]
    public async Task LinkParentAccountCommand_ReturnsNotFound_WhenTargetEmailDoesNotExist()
    {
        await using var db = CreateDbContext();
        var parent = CreateUser("parent@example.com", UserRole.Parent);
        db.Users.Add(parent);
        await db.SaveChangesAsync();

        var handler = new LinkParentAccountCommandHandler(db, new FakeEmailService());
        var result = await handler.Handle(
            new LinkParentAccountCommand(parent.Id, "nobody@example.com"),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
    }

    [Fact]
    public async Task LinkParentAccountCommand_ReturnsBadRequest_WhenLinkingSelf()
    {
        await using var db = CreateDbContext();
        var parent = CreateUser("parent@example.com", UserRole.Parent);
        db.Users.Add(parent);
        await db.SaveChangesAsync();

        var handler = new LinkParentAccountCommandHandler(db, new FakeEmailService());
        var result = await handler.Handle(
            new LinkParentAccountCommand(parent.Id, "parent@example.com"),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
    }

    [Fact]
    public async Task LinkParentAccountCommand_ReturnsBadRequest_WhenAlreadyLinked()
    {
        await using var db = CreateDbContext();

        var parentA = CreateUser("a@example.com", UserRole.Parent);
        var parentB = CreateUser("b@example.com", UserRole.Parent);
        db.Users.AddRange(parentA, parentB);

        var (aId, bId) = ApiEndpointHelpers.NormalizeParentLinkPair(parentA.Id, parentB.Id);
        db.ParentAccountLinks.Add(new ParentAccountLink { ParentAId = aId, ParentBId = bId, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var handler = new LinkParentAccountCommandHandler(db, new FakeEmailService());
        var result = await handler.Handle(
            new LinkParentAccountCommand(parentA.Id, "b@example.com"),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Contains("already linked", result.Error);
    }

    // ── UnlinkParentAccountCommand ──────────────────────────────────────────

    [Fact]
    public async Task UnlinkParentAccountCommand_RemovesLink()
    {
        await using var db = CreateDbContext();

        var parentA = CreateUser("a@example.com", UserRole.Parent);
        var parentB = CreateUser("b@example.com", UserRole.Parent);
        db.Users.AddRange(parentA, parentB);

        var (aId, bId) = ApiEndpointHelpers.NormalizeParentLinkPair(parentA.Id, parentB.Id);
        db.ParentAccountLinks.Add(new ParentAccountLink { ParentAId = aId, ParentBId = bId, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var handler = new UnlinkParentAccountCommandHandler(db);
        var result = await handler.Handle(
            new UnlinkParentAccountCommand(parentA.Id, parentB.Id),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status204NoContent, result.StatusCode);
        Assert.Equal(0, await db.ParentAccountLinks.CountAsync());
    }

    [Fact]
    public async Task UnlinkParentAccountCommand_ReturnsNotFound_WhenLinkMissing()
    {
        await using var db = CreateDbContext();

        var handler = new UnlinkParentAccountCommandHandler(db);
        var result = await handler.Handle(
            new UnlinkParentAccountCommand(Guid.NewGuid(), Guid.NewGuid()),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
    }

    [Fact]
    public async Task UnlinkParentAccountCommand_ReturnsBadRequest_WhenUnlinkingSelf()
    {
        await using var db = CreateDbContext();
        var id = Guid.NewGuid();

        var handler = new UnlinkParentAccountCommandHandler(db);
        var result = await handler.Handle(
            new UnlinkParentAccountCommand(id, id),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"manage-handler-tests-{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    private static AppUser CreateUser(string email, UserRole role)
    {
        return new AppUser
        {
            Email = email,
            PasswordHash = "hash",
            Role = role,
            CreatedAt = DateTime.UtcNow,
        };
    }

    private sealed class FakeEmailService : IEmailService
    {
        public bool ParentLinkedEmailSent { get; private set; }

        public Task<bool> SendInvitationAsync(string toEmail, string? displayName, string inviterName)
            => Task.FromResult(true);

        public Task<bool> SendParentLinkedAsync(string toEmail, string? displayName, string linkedByEmail)
        {
            ParentLinkedEmailSent = true;
            return Task.FromResult(true);
        }

        public Task<bool> SendFriendInviteAsync(string toEmail, string inviterName, string inviteUrl)
            => Task.FromResult(true);
    }
}
