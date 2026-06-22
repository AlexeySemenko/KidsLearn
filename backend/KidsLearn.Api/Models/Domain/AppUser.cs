public class AppUser
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? ExternalProvider { get; set; }
    public string? ExternalSubject { get; set; }
    public bool EmailVerified { get; set; }
    public UserRole Role { get; set; } = UserRole.Parent;
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastAccessAt { get; set; }

    public ICollection<Child> Children { get; set; } = new List<Child>();
    public Child? ChildProfile { get; set; }
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}