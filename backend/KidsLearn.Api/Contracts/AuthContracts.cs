public sealed record RegisterRequest(string Email, string Password);

public sealed record RegisterChildRequest(string Email, string Password);

public sealed record LoginRequest(string Email, string Password);

public sealed record ChildLoginRequest(Guid ChildId, string AccessCode);

public sealed record RefreshRequest(string RefreshToken);

public sealed record RevokeRequest(string RefreshToken);

public sealed record GoogleFinalizeRequest(string AuthCode);

public sealed record ChildGoogleFinalizeRequest(string AuthCode);

public sealed record AuthUserResponse(Guid Id, string Email, string Role, string? DisplayName, string? AvatarUrl);

public sealed record AuthTokenResponse(string AccessToken, string RefreshToken, int ExpiresInSeconds, AuthUserResponse User);