using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Xunit;

public class FriendCommandHandlersUnitTests
{
    // ── SendChildFriendInviteCommand ────────────────────────────────────────

    [Fact]
    public async Task SendChildFriendInviteCommand_CreatesInvite_AndSendsEmail()
    {
        await using var db = CreateDbContext();
        var email = new FakeEmailService();

        var parent = CreateUser("parent@example.com", UserRole.Parent);
        var childA = CreateChild(parent, "Alice", "alice@example.com");
        db.Users.AddRange(parent, childA.User!);
        db.Children.Add(childA);
        await db.SaveChangesAsync();

        var handler = new SendChildFriendInviteCommandHandler(db, email);
        var result = await handler.Handle(
            new SendChildFriendInviteCommand(childA.Id, "bob@example.com", "https://app.example.com"),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Null(result.Error);
        Assert.Equal(1, await db.ChildFriendships.CountAsync());
        Assert.True(email.FriendInviteSent);

        var friendship = await db.ChildFriendships.SingleAsync();
        Assert.Equal("bob@example.com", friendship.InviteeEmail);
        Assert.Equal("Pending", friendship.Status);
        Assert.NotEmpty(friendship.InviteToken);
    }

    [Fact]
    public async Task SendChildFriendInviteCommand_ReturnsBadRequest_WhenInvitingSelf()
    {
        await using var db = CreateDbContext();

        var parent = CreateUser("parent@example.com", UserRole.Parent);
        var child = CreateChild(parent, "Alice", "alice@example.com");
        db.Users.AddRange(parent, child.User!);
        db.Children.Add(child);
        await db.SaveChangesAsync();

        var handler = new SendChildFriendInviteCommandHandler(db, new FakeEmailService());
        var result = await handler.Handle(
            new SendChildFriendInviteCommand(child.Id, "alice@example.com", "https://app.example.com"),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal(0, await db.ChildFriendships.CountAsync());
    }

    [Fact]
    public async Task SendChildFriendInviteCommand_ReturnsConflict_WhenAlreadyFriends()
    {
        await using var db = CreateDbContext();

        var parent = CreateUser("parent@example.com", UserRole.Parent);
        var childA = CreateChild(parent, "Alice", "alice@example.com");
        var childB = CreateChild(parent, "Bob", "bob@example.com");
        db.Users.AddRange(parent, childA.User!, childB.User!);
        db.Children.AddRange(childA, childB);

        db.ChildFriendships.Add(new ChildFriendship
        {
            RequesterId = childA.Id,
            Requester = childA,
            AcceptorId = childB.Id,
            Acceptor = childB,
            InviteeEmail = "bob@example.com",
            InviteToken = "existing-token",
            Status = "Accepted",
            CreatedAt = DateTime.UtcNow,
            AcceptedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();

        var handler = new SendChildFriendInviteCommandHandler(db, new FakeEmailService());
        var result = await handler.Handle(
            new SendChildFriendInviteCommand(childA.Id, "bob@example.com", "https://app.example.com"),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Equal(1, await db.ChildFriendships.CountAsync());
    }

    [Fact]
    public async Task SendChildFriendInviteCommand_ReturnsConflict_WhenPendingInviteExists()
    {
        await using var db = CreateDbContext();

        var parent = CreateUser("parent@example.com", UserRole.Parent);
        var child = CreateChild(parent, "Alice", "alice@example.com");
        db.Users.AddRange(parent, child.User!);
        db.Children.Add(child);

        db.ChildFriendships.Add(new ChildFriendship
        {
            RequesterId = child.Id,
            Requester = child,
            InviteeEmail = "bob@example.com",
            InviteToken = "pending-token",
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();

        var handler = new SendChildFriendInviteCommandHandler(db, new FakeEmailService());
        var result = await handler.Handle(
            new SendChildFriendInviteCommand(child.Id, "bob@example.com", "https://app.example.com"),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
        Assert.Equal(1, await db.ChildFriendships.CountAsync());
    }

    // ── GetChildFriendInviteQuery ───────────────────────────────────────────

    [Fact]
    public async Task GetChildFriendInviteQuery_ReturnsInviteInfo()
    {
        await using var db = CreateDbContext();

        var parent = CreateUser("parent@example.com", UserRole.Parent);
        var child = CreateChild(parent, "Alice", "alice@example.com");
        db.Users.AddRange(parent, child.User!);
        db.Children.Add(child);

        const string token = "test-token-abc";
        db.ChildFriendships.Add(new ChildFriendship
        {
            RequesterId = child.Id,
            Requester = child,
            InviteeEmail = "bob@example.com",
            InviteToken = token,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();

        var handler = new GetChildFriendInviteQueryHandler(db);
        var result = await handler.Handle(new GetChildFriendInviteQuery(token), CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.NotNull(result.Response);
        Assert.Equal("Alice", result.Response!.RequesterName);
        Assert.Equal("Pending", result.Response.Status);
    }

    [Fact]
    public async Task GetChildFriendInviteQuery_ReturnsNotFound_WhenTokenInvalid()
    {
        await using var db = CreateDbContext();

        var handler = new GetChildFriendInviteQueryHandler(db);
        var result = await handler.Handle(new GetChildFriendInviteQuery("no-such-token"), CancellationToken.None);

        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
    }

    // ── AcceptChildFriendInviteCommand ─────────────────────────────────────

    [Fact]
    public async Task AcceptChildFriendInviteCommand_AcceptsInvite_AndSetsFriendship()
    {
        await using var db = CreateDbContext();

        var parent = CreateUser("parent@example.com", UserRole.Parent);
        var childA = CreateChild(parent, "Alice", "alice@example.com");
        var childB = CreateChild(parent, "Bob", "bob@example.com");
        db.Users.AddRange(parent, childA.User!, childB.User!);
        db.Children.AddRange(childA, childB);

        const string token = "accept-token";
        db.ChildFriendships.Add(new ChildFriendship
        {
            RequesterId = childA.Id,
            Requester = childA,
            InviteeEmail = "bob@example.com",
            InviteToken = token,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();

        var handler = new AcceptChildFriendInviteCommandHandler(db);
        var result = await handler.Handle(
            new AcceptChildFriendInviteCommand(childB.Id, token),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.NotNull(result.Friend);
        Assert.Equal(childA.Id, result.Friend!.ChildId);

        var friendship = await db.ChildFriendships.SingleAsync();
        Assert.Equal("Accepted", friendship.Status);
        Assert.Equal(childB.Id, friendship.AcceptorId);
        Assert.NotNull(friendship.AcceptedAt);
    }

    [Fact]
    public async Task AcceptChildFriendInviteCommand_ReturnsNotFound_WhenTokenInvalid()
    {
        await using var db = CreateDbContext();

        var handler = new AcceptChildFriendInviteCommandHandler(db);
        var result = await handler.Handle(
            new AcceptChildFriendInviteCommand(Guid.NewGuid(), "no-such-token"),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
    }

    [Fact]
    public async Task AcceptChildFriendInviteCommand_ReturnsConflict_WhenAlreadyAccepted()
    {
        await using var db = CreateDbContext();

        var parent = CreateUser("parent@example.com", UserRole.Parent);
        var childA = CreateChild(parent, "Alice", "alice@example.com");
        var childB = CreateChild(parent, "Bob", "bob@example.com");
        db.Users.AddRange(parent, childA.User!, childB.User!);
        db.Children.AddRange(childA, childB);

        const string token = "already-done-token";
        db.ChildFriendships.Add(new ChildFriendship
        {
            RequesterId = childA.Id,
            Requester = childA,
            AcceptorId = childB.Id,
            Acceptor = childB,
            InviteeEmail = "bob@example.com",
            InviteToken = token,
            Status = "Accepted",
            CreatedAt = DateTime.UtcNow,
            AcceptedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();

        var handler = new AcceptChildFriendInviteCommandHandler(db);
        var result = await handler.Handle(
            new AcceptChildFriendInviteCommand(childB.Id, token),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
    }

    [Fact]
    public async Task AcceptChildFriendInviteCommand_ReturnsBadRequest_WhenAcceptingOwnInvite()
    {
        await using var db = CreateDbContext();

        var parent = CreateUser("parent@example.com", UserRole.Parent);
        var child = CreateChild(parent, "Alice", "alice@example.com");
        db.Users.AddRange(parent, child.User!);
        db.Children.Add(child);

        const string token = "own-token";
        db.ChildFriendships.Add(new ChildFriendship
        {
            RequesterId = child.Id,
            Requester = child,
            InviteeEmail = "someone@example.com",
            InviteToken = token,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();

        var handler = new AcceptChildFriendInviteCommandHandler(db);
        var result = await handler.Handle(
            new AcceptChildFriendInviteCommand(child.Id, token),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
    }

    // ── GetChildFriendsQuery ────────────────────────────────────────────────

    [Fact]
    public async Task GetChildFriendsQuery_ReturnsFriends_FromBothSides()
    {
        await using var db = CreateDbContext();

        var parent = CreateUser("parent@example.com", UserRole.Parent);
        var childA = CreateChild(parent, "Alice", "alice@example.com");
        var childB = CreateChild(parent, "Bob", "bob@example.com");
        var childC = CreateChild(parent, "Carol", "carol@example.com");
        db.Users.AddRange(parent, childA.User!, childB.User!, childC.User!);
        db.Children.AddRange(childA, childB, childC);

        // A invited B (A is requester, B is acceptor)
        db.ChildFriendships.Add(new ChildFriendship
        {
            RequesterId = childA.Id,
            Requester = childA,
            AcceptorId = childB.Id,
            Acceptor = childB,
            InviteeEmail = "bob@example.com",
            InviteToken = "token-ab",
            Status = "Accepted",
            CreatedAt = DateTime.UtcNow,
            AcceptedAt = DateTime.UtcNow
        });

        // C invited A (C is requester, A is acceptor)
        db.ChildFriendships.Add(new ChildFriendship
        {
            RequesterId = childC.Id,
            Requester = childC,
            AcceptorId = childA.Id,
            Acceptor = childA,
            InviteeEmail = "alice@example.com",
            InviteToken = "token-ca",
            Status = "Accepted",
            CreatedAt = DateTime.UtcNow,
            AcceptedAt = DateTime.UtcNow
        });

        // Pending invite — should NOT appear
        db.ChildFriendships.Add(new ChildFriendship
        {
            RequesterId = childA.Id,
            Requester = childA,
            InviteeEmail = "pending@example.com",
            InviteToken = "token-pending",
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();

        var handler = new GetChildFriendsQueryHandler(db);
        var result = await handler.Handle(new GetChildFriendsQuery(childA.Id), CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, f => f.ChildId == childB.Id);
        Assert.Contains(result, f => f.ChildId == childC.Id);
    }

    [Fact]
    public async Task GetChildFriendsQuery_ReturnsEmpty_WhenNoFriends()
    {
        await using var db = CreateDbContext();

        var parent = CreateUser("parent@example.com", UserRole.Parent);
        var child = CreateChild(parent, "Alice", "alice@example.com");
        db.Users.AddRange(parent, child.User!);
        db.Children.Add(child);
        await db.SaveChangesAsync();

        var handler = new GetChildFriendsQueryHandler(db);
        var result = await handler.Handle(new GetChildFriendsQuery(child.Id), CancellationToken.None);

        Assert.Empty(result);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"friend-handler-tests-{Guid.NewGuid():N}")
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

    private static Child CreateChild(AppUser parent, string name, string email)
    {
        var user = CreateUser(email, UserRole.Child);
        return new Child
        {
            Parent = parent,
            ParentId = parent.Id,
            User = user,
            UserId = user.Id,
            Name = name,
            Grade = 3
        };
    }

    private sealed class FakeEmailService : IEmailService
    {
        public bool FriendInviteSent { get; private set; }

        public Task<bool> SendInvitationAsync(string toEmail, string? displayName, string inviterName)
            => Task.FromResult(true);

        public Task<bool> SendParentLinkedAsync(string toEmail, string? displayName, string linkedByEmail)
            => Task.FromResult(true);

        public Task<bool> SendFriendInviteAsync(string toEmail, string inviterName, string inviteUrl)
        {
            FriendInviteSent = true;
            return Task.FromResult(true);
        }

        public Task<bool> SendAssignmentCompletedToParentAsync(string toEmail, string parentName, string childName, string lessonTitle, decimal score, int correctAnswers, int totalQuestions, IList<(string LessonTitle, decimal Score)> recentResults)
            => Task.FromResult(true);

        public Task<bool> SendAssignmentCreatedToChildAsync(string toEmail, string childName, string lessonTitle, string subject, DateTime? dueDate)
            => Task.FromResult(true);

        public Task<bool> SendWelcomeToParentAsync(string toEmail, string? displayName)
            => Task.FromResult(true);

        public Task<bool> SendChildAddedToParentAsync(string toEmail, string? parentName, string childName, int grade)
            => Task.FromResult(true);

        public Task<bool> SendChildWelcomeAsync(string toEmail, string childName, string parentEmail, string registerUrl)
            => Task.FromResult(true);
    }
}
