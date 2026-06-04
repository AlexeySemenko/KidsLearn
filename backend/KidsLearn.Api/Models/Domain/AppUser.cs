public class AppUser
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Parent;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Child> Children { get; set; } = new List<Child>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}