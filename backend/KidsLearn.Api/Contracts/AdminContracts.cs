public sealed record AdminUserResponse(
    Guid Id,
    string Email,
    string? DisplayName,
    string Role,
    bool EmailVerified,
    string? ExternalProvider,
    DateTime CreatedAt,
    DateTime? LastAccessAt);

public sealed record AdminCreateUserRequest(
    string Email,
    string? DisplayName,
    string Role);

public sealed record AdminUpdateUserRequest(
    string? DisplayName,
    string? Role);

public sealed record AdminCreateUserResponse(AdminUserResponse User, bool EmailSent);
