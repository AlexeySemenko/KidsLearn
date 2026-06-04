public sealed record RegisterRequest(string Email, string Password);

public sealed record LoginRequest(string Email, string Password);

public sealed record RefreshRequest(string RefreshToken);

public sealed record RevokeRequest(string RefreshToken);

public sealed record AuthUserResponse(Guid Id, string Email, string Role);

public sealed record AuthTokenResponse(string AccessToken, string RefreshToken, int ExpiresInSeconds, AuthUserResponse User);