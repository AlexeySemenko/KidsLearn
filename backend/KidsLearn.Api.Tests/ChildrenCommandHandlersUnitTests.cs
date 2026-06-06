using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Xunit;

public class ChildrenCommandHandlersUnitTests
{
    [Fact]
    public async Task GetParentChildrenQuery_ReturnsOnlyChildrenOfParent()
    {
        await using var db = CreateDbContext();

        var parentA = CreateUser("parent-a@example.com", UserRole.Parent);
        var parentB = CreateUser("parent-b@example.com", UserRole.Parent);
        db.Users.AddRange(parentA, parentB);

        db.Children.Add(new Child { ParentId = parentA.Id, Name = "A-Child-1", Grade = 2 });
        db.Children.Add(new Child { ParentId = parentA.Id, Name = "A-Child-2", Grade = 3 });
        db.Children.Add(new Child { ParentId = parentB.Id, Name = "B-Child-1", Grade = 4 });
        await db.SaveChangesAsync();

        var handler = new GetParentChildrenQueryHandler(db);
        var result = await handler.Handle(new GetParentChildrenQuery(parentA.Id), CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.All(result, x => Assert.Equal(parentA.Id, x.ParentId));
    }

    [Fact]
    public async Task CreateParentChildCommand_CreatesChildAndChildUser_WhenValid()
    {
        await using var db = CreateDbContext();
        var hasher = new PasswordHasherService();

        var parent = CreateUser("parent@example.com", UserRole.Parent);
        db.Users.Add(parent);
        await db.SaveChangesAsync();

        var handler = new CreateParentChildCommandHandler(db, hasher);
        var result = await handler.Handle(
            new CreateParentChildCommand(parent.Id, new CreateChildRequest("Kid", 5, "1234")),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        Assert.NotNull(result.Response);
        Assert.Equal("Kid", result.Response!.Child.Name);
        Assert.Equal(1, await db.Children.CountAsync(x => x.ParentId == parent.Id));

        var child = await db.Children.Include(x => x.User).SingleAsync(x => x.ParentId == parent.Id);
        Assert.NotNull(child.User);
        Assert.Equal(UserRole.Child, child.User!.Role);
        Assert.True(hasher.VerifyPassword("1234", child.User.PasswordHash));
    }

    [Fact]
    public async Task CreateParentChildCommand_ReturnsBadRequest_WhenGradeOutOfRange()
    {
        await using var db = CreateDbContext();
        var parent = CreateUser("parent@example.com", UserRole.Parent);
        db.Users.Add(parent);
        await db.SaveChangesAsync();

        var handler = new CreateParentChildCommandHandler(db, new PasswordHasherService());
        var result = await handler.Handle(
            new CreateParentChildCommand(parent.Id, new CreateChildRequest("Kid", 0, "1234")),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status400BadRequest, result.StatusCode);
        Assert.Equal("Grade must be between 1 and 12.", result.Error);
    }

    [Fact]
    public async Task UpdateParentChildCommand_UpdatesNameGradeAndAccessCode()
    {
        await using var db = CreateDbContext();
        var hasher = new PasswordHasherService();

        var parent = CreateUser("parent@example.com", UserRole.Parent);
        var childUser = CreateUser("child@example.com", UserRole.Child, hasher.HashPassword("1111"));
        var child = new Child
        {
            ParentId = parent.Id,
            UserId = childUser.Id,
            Parent = parent,
            User = childUser,
            Name = "Old Name",
            Grade = 2
        };

        db.Users.AddRange(parent, childUser);
        db.Children.Add(child);
        await db.SaveChangesAsync();

        var oldHash = childUser.PasswordHash;

        var handler = new UpdateParentChildCommandHandler(db, hasher);
        var result = await handler.Handle(
            new UpdateParentChildCommand(parent.Id, child.Id, new UpdateChildRequest("New Name", 6, "9999")),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.NotNull(result.Response);
        Assert.Equal("New Name", result.Response!.Name);
        Assert.Equal(6, result.Response.Grade);

        var updatedChild = await db.Children.Include(x => x.User).SingleAsync(x => x.Id == child.Id);
        Assert.Equal("New Name", updatedChild.Name);
        Assert.Equal(6, updatedChild.Grade);
        Assert.NotEqual(oldHash, updatedChild.User!.PasswordHash);
        Assert.True(hasher.VerifyPassword("9999", updatedChild.User.PasswordHash));
    }

    [Fact]
    public async Task ResetParentChildAccessCodeCommand_ReplacesPasswordHash()
    {
        await using var db = CreateDbContext();
        var hasher = new PasswordHasherService();

        var parent = CreateUser("parent@example.com", UserRole.Parent);
        var childUser = CreateUser("child@example.com", UserRole.Child, hasher.HashPassword("1111"));
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

        var oldHash = childUser.PasswordHash;
        var handler = new ResetParentChildAccessCodeCommandHandler(db, hasher);

        var result = await handler.Handle(
            new ResetParentChildAccessCodeCommand(parent.Id, child.Id),
            CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.NotNull(result.Response);
        Assert.Equal(child.Id, result.Response!.ChildId);
        Assert.True(result.Response.AccessCode.Length >= 4);

        var updatedUser = await db.Users.SingleAsync(x => x.Id == childUser.Id);
        Assert.NotEqual(oldHash, updatedUser.PasswordHash);
        Assert.True(hasher.VerifyPassword(result.Response.AccessCode, updatedUser.PasswordHash));
    }

    [Fact]
    public async Task DeleteParentChildCommand_RemovesChildAndLinkedChildUser()
    {
        await using var db = CreateDbContext();

        var parent = CreateUser("parent@example.com", UserRole.Parent);
        var childUser = CreateUser("child@example.com", UserRole.Child);
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

        var handler = new DeleteParentChildCommandHandler(db);
        var result = await handler.Handle(new DeleteParentChildCommand(parent.Id, child.Id), CancellationToken.None);

        Assert.Equal(StatusCodes.Status204NoContent, result.StatusCode);
        Assert.False(await db.Children.AnyAsync(x => x.Id == child.Id));
        Assert.False(await db.Users.AnyAsync(x => x.Id == childUser.Id));
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"children-handler-tests-{Guid.NewGuid():N}")
            .Options;

        return new AppDbContext(options);
    }

    private static AppUser CreateUser(string email, UserRole role, string? passwordHash = null)
    {
        return new AppUser
        {
            Email = email,
            PasswordHash = passwordHash ?? "hash",
            Role = role,
            CreatedAt = DateTime.UtcNow
        };
    }
}
