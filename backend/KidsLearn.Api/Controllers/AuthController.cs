using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MediatR;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

public static class AuthController
{
    private const string GoogleProviderName = "Google";
    private const int GoogleAuthCodeTtlMinutes = 10;

    public static RouteGroupBuilder MapAuthController(this RouteGroupBuilder apiV1)
    {
        apiV1.MapPost("/auth/register", async (ISender sender, RegisterRequest request) =>
        {
            var result = await sender.Send(new RegisterParentCommand(request));
            return result.StatusCode switch
            {
                StatusCodes.Status201Created when result.User is not null
                    => Results.Created($"/api/v1/users/{result.User.Id}", result.User),
                StatusCodes.Status400BadRequest
                    => Results.BadRequest(new { error = result.Error ?? "Bad request." }),
                StatusCodes.Status409Conflict
                    => Results.Conflict(new { error = result.Error ?? "Conflict." }),
                _ => Results.Problem(result.Error ?? "Unexpected error.")
            };
        });

        apiV1.MapPost("/auth/child/register", async (ISender sender, RegisterChildRequest request) =>
        {
            var result = await sender.Send(new RegisterChildCommand(request));
            return result.StatusCode switch
            {
                StatusCodes.Status201Created when result.Response is not null
                    => Results.Ok(result.Response),
                StatusCodes.Status400BadRequest
                    => Results.BadRequest(new { error = result.Error ?? "Bad request." }),
                StatusCodes.Status409Conflict
                    => Results.Conflict(new { error = result.Error ?? "Conflict." }),
                _ => Results.Problem(result.Error ?? "Unexpected error.")
            };
        }).AllowAnonymous();

        apiV1.MapPost("/auth/login", async (ISender sender, LoginRequest request) =>
        {
            var result = await sender.Send(new LoginParentCommand(request));
            return result.StatusCode switch
            {
                StatusCodes.Status200OK when result.Response is not null => Results.Ok(result.Response),
                StatusCodes.Status400BadRequest => Results.BadRequest(new { error = result.Error ?? "Bad request." }),
                StatusCodes.Status401Unauthorized => Results.Unauthorized(),
                _ => Results.Problem(result.Error ?? "Unexpected error.")
            };
        });

        apiV1.MapPost("/auth/child-login", async (ISender sender, ChildLoginRequest request) =>
        {
            var result = await sender.Send(new LoginChildCommand(request));
            return result.StatusCode switch
            {
                StatusCodes.Status200OK when result.Response is not null => Results.Ok(result.Response),
                StatusCodes.Status400BadRequest => Results.BadRequest(new { error = result.Error ?? "Bad request." }),
                StatusCodes.Status401Unauthorized => Results.Unauthorized(),
                _ => Results.Problem(result.Error ?? "Unexpected error.")
            };
        });

        apiV1.MapPost("/auth/refresh", async (ISender sender, RefreshRequest request) =>
        {
            var result = await sender.Send(new RefreshAuthTokenCommand(request));
            return result.StatusCode switch
            {
                StatusCodes.Status200OK when result.Response is not null => Results.Ok(result.Response),
                StatusCodes.Status400BadRequest => Results.BadRequest(new { error = result.Error ?? "Bad request." }),
                StatusCodes.Status401Unauthorized => Results.Unauthorized(),
                _ => Results.Problem(result.Error ?? "Unexpected error.")
            };
        });

        apiV1.MapPost("/auth/revoke", async (ISender sender, RevokeRequest request) =>
        {
            var result = await sender.Send(new RevokeAuthTokenCommand(request));
            return result.StatusCode switch
            {
                StatusCodes.Status204NoContent => Results.NoContent(),
                StatusCodes.Status400BadRequest => Results.BadRequest(new { error = result.Error ?? "Bad request." }),
                StatusCodes.Status404NotFound => Results.NotFound(),
                _ => Results.Problem(result.Error ?? "Unexpected error.")
            };
        });

        apiV1.MapGet("/auth/google/start", (HttpContext httpContext, IConfiguration configuration) =>
        {
            var clientId = configuration["GoogleAuth:ClientId"];
            var redirectUri = configuration["GoogleAuth:RedirectUri"];
            var frontendCallbackUrl = configuration["GoogleAuth:ParentFrontendCallbackUrl"];

            if (string.IsNullOrWhiteSpace(clientId)
                || string.IsNullOrWhiteSpace(redirectUri)
                || string.IsNullOrWhiteSpace(frontendCallbackUrl))
            {
                return Results.Problem(
                    title: "Google auth is not configured.",
                    detail: "Set GoogleAuth:ClientId, GoogleAuth:RedirectUri, and GoogleAuth:FrontendCallbackUrl.",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            var returnPath = httpContext.Request.Query["returnPath"].ToString();
            if (!IsValidReturnPath(returnPath))
            {
                returnPath = "/parent";
            }

            var jwtSigningKey = configuration["Jwt:Key"];
            if (string.IsNullOrWhiteSpace(jwtSigningKey) && string.Equals(configuration["ASPNETCORE_ENVIRONMENT"], "Development", StringComparison.OrdinalIgnoreCase))
            {
                jwtSigningKey = "dev-super-secret-key-change-in-production-32chars";
            }

            if (string.IsNullOrWhiteSpace(jwtSigningKey))
            {
                return Results.Problem("Jwt:Key is not configured.", statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            var state = CreateSignedStateToken(new GoogleAuthStateContext(frontendCallbackUrl, returnPath), jwtSigningKey);

            var authorizationUrl = QueryHelpers.AddQueryString("https://accounts.google.com/o/oauth2/v2/auth", new Dictionary<string, string?>
            {
                ["client_id"] = clientId,
                ["redirect_uri"] = redirectUri,
                ["response_type"] = "code",
                ["scope"] = "openid email profile",
                ["state"] = state,
                ["prompt"] = "select_account",
            });

            return Results.Redirect(authorizationUrl);
        });

        apiV1.MapGet("/auth/google/callback", async (
            string? code,
            string? state,
            string? error,
            IMemoryCache cache,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
            AppDbContext db,
            IJwtTokenService tokenService,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) =>
        {
            var logger = loggerFactory.CreateLogger("GoogleAuth");
            var defaultFrontendCallback = configuration["GoogleAuth:ParentFrontendCallbackUrl"];

            if (string.IsNullOrWhiteSpace(defaultFrontendCallback))
            {
                return Results.Problem(
                    title: "Google auth is not configured.",
                    detail: "Set GoogleAuth:ParentFrontendCallbackUrl.",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            if (!string.IsNullOrWhiteSpace(error))
            {
                return Results.Redirect(BuildFrontendCallbackUrl(defaultFrontendCallback, "google_access_denied", null, "/parent"));
            }

            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
            {
                return Results.Redirect(BuildFrontendCallbackUrl(defaultFrontendCallback, "google_invalid_callback", null, "/parent"));
            }

            var jwtSigningKey = configuration["Jwt:Key"];
            if (string.IsNullOrWhiteSpace(jwtSigningKey) && string.Equals(configuration["ASPNETCORE_ENVIRONMENT"], "Development", StringComparison.OrdinalIgnoreCase))
            {
                jwtSigningKey = "dev-super-secret-key-change-in-production-32chars";
            }

            if (!string.IsNullOrWhiteSpace(jwtSigningKey) && TryReadSignedStateToken(state, jwtSigningKey, out var authState) && authState is not null)
            {
                var frontendCallbackUrl = authState.FrontendCallbackUrl;
                var returnPath = IsValidReturnPath(authState.ReturnPath) ? authState.ReturnPath : "/parent";

                var clientId = configuration["GoogleAuth:ClientId"];
                var clientSecret = configuration["GoogleAuth:ClientSecret"];
                var redirectUri = configuration["GoogleAuth:RedirectUri"];

                if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret) || string.IsNullOrWhiteSpace(redirectUri))
                {
                    return Results.Redirect(BuildFrontendCallbackUrl(frontendCallbackUrl, "google_not_configured", null, returnPath));
                }

                var httpClient = httpClientFactory.CreateClient();
                // Some proxies/frameworks can decode '+' in query values as space.
                // Google auth codes may contain '+', so normalize before token exchange.
                var normalizedCode = code.Replace(" ", "+", StringComparison.Ordinal);

                var tokenResponse = await ExchangeGoogleCodeAsync(httpClient, normalizedCode, clientId, clientSecret, redirectUri, logger, cancellationToken);
                if (tokenResponse is null || string.IsNullOrWhiteSpace(tokenResponse.AccessToken))
                {
                    logger.LogWarning("Google token exchange failed.");
                    return Results.Redirect(BuildFrontendCallbackUrl(frontendCallbackUrl, "google_exchange_failed", null, returnPath));
                }

                var profile = await GetGoogleProfileAsync(httpClient, tokenResponse.AccessToken, cancellationToken);
                if (profile is null || string.IsNullOrWhiteSpace(profile.Sub) || string.IsNullOrWhiteSpace(profile.Email))
                {
                    logger.LogWarning("Google profile fetch failed or missing required claims.");
                    return Results.Redirect(BuildFrontendCallbackUrl(frontendCallbackUrl, "google_profile_invalid", null, returnPath));
                }

            if (!profile.EmailVerified)
            {
                return Results.Redirect(BuildFrontendCallbackUrl(frontendCallbackUrl, "google_email_not_verified", null, returnPath));
            }

            var normalizedEmail = profile.Email.Trim().ToLowerInvariant();

            var user = await db.Users.FirstOrDefaultAsync(
                x => x.ExternalProvider == GoogleProviderName && x.ExternalSubject == profile.Sub,
                cancellationToken);

            var adminEmails = configuration.GetSection("AdminEmails").Get<string[]>() ?? Array.Empty<string>();
            var isAdminEmail = adminEmails.Any(e => string.Equals(e.Trim(), normalizedEmail, StringComparison.OrdinalIgnoreCase));

            // Unified flow: if the account belongs to a child, log them in and return early
            if (authState.IsUnified)
            {
                AppUser? childAccount = null;
                if (user?.Role == UserRole.Child)
                {
                    childAccount = user;
                }
                else if (user is null)
                {
                    childAccount = await db.Users.FirstOrDefaultAsync(
                        x => x.Email == normalizedEmail && x.Role == UserRole.Child, cancellationToken);
                }

                if (childAccount is not null)
                {
                    var childRecord = await db.Children.AsNoTracking()
                        .FirstOrDefaultAsync(x => x.UserId == childAccount.Id, cancellationToken);
                    if (childRecord is null)
                        return Results.Redirect(BuildFrontendCallbackUrl(frontendCallbackUrl, "child_not_registered", null, returnPath));

                    if (childAccount.ExternalProvider != GoogleProviderName || childAccount.ExternalSubject != profile.Sub
                        || (!string.IsNullOrWhiteSpace(profile.Picture) && childAccount.AvatarUrl != profile.Picture))
                    {
                        childAccount.ExternalProvider = GoogleProviderName;
                        childAccount.ExternalSubject = profile.Sub;
                        childAccount.EmailVerified = true;
                        if (!string.IsNullOrWhiteSpace(profile.Picture)) childAccount.AvatarUrl = profile.Picture;
                        db.Users.Update(childAccount);
                    }
                    childAccount.LastAccessAt = DateTime.UtcNow;

                    var cAccessToken = tokenService.CreateAccessToken(childAccount);
                    var cRefreshToken = tokenService.CreateRefreshToken();
                    var cRefreshDays = int.TryParse(configuration["Jwt:RefreshTokenExpirationDays"], out var crd) ? crd : 14;
                    db.RefreshTokens.Add(new RefreshToken
                    {
                        UserId = childAccount.Id, Token = cRefreshToken,
                        CreatedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddDays(cRefreshDays)
                    });
                    await db.SaveChangesAsync(cancellationToken);

                    var cExpiresIn = int.TryParse(configuration["Jwt:AccessTokenExpirationMinutes"], out var cam) ? cam * 60 : 1800;
                    var cAuthResponse = new AuthTokenResponse(cAccessToken, cRefreshToken, cExpiresIn,
                        new AuthUserResponse(childAccount.Id, childAccount.Email, childAccount.Role.ToString(), childRecord.Name, childAccount.AvatarUrl));
                    var cAuthCode = CreateSignedAuthCode(cAuthResponse, jwtSigningKey);
                    return Results.Redirect(BuildFrontendCallbackUrl(frontendCallbackUrl, null, cAuthCode, returnPath));
                }
                // No child found — fall through to parent / create-parent logic below
            }

            if (user is not null && user.Role != UserRole.Parent && user.Role != UserRole.Admin)
            {
                return Results.Redirect(BuildFrontendCallbackUrl(frontendCallbackUrl, "google_role_not_allowed", null, returnPath));
            }

            if (user is null)
            {
                var userByEmail = await db.Users.FirstOrDefaultAsync(x => x.Email == normalizedEmail, cancellationToken);
                if (userByEmail is not null)
                {
                    if (userByEmail.Role != UserRole.Parent && userByEmail.Role != UserRole.Admin)
                    {
                        return Results.Redirect(BuildFrontendCallbackUrl(frontendCallbackUrl, "google_role_not_allowed", null, returnPath));
                    }

                    var hasExternalIdentity = !string.IsNullOrWhiteSpace(userByEmail.ExternalProvider)
                        || !string.IsNullOrWhiteSpace(userByEmail.ExternalSubject);
                    var canLinkGoogle = userByEmail.ExternalProvider == GoogleProviderName
                        && (string.IsNullOrWhiteSpace(userByEmail.ExternalSubject) || userByEmail.ExternalSubject == profile.Sub);

                    if (hasExternalIdentity && !canLinkGoogle)
                    {
                        return Results.Redirect(BuildFrontendCallbackUrl(frontendCallbackUrl, "google_link_conflict", null, returnPath));
                    }

                    userByEmail.ExternalProvider = GoogleProviderName;
                    userByEmail.ExternalSubject = profile.Sub;
                    userByEmail.EmailVerified = true;
                    if (!string.IsNullOrWhiteSpace(profile.Name)) userByEmail.DisplayName = profile.Name;
                    if (!string.IsNullOrWhiteSpace(profile.Picture)) userByEmail.AvatarUrl = profile.Picture;
                    if (isAdminEmail) userByEmail.Role = UserRole.Admin;
                    user = userByEmail;
                }
                else
                {
                    user = new AppUser
                    {
                        Email = normalizedEmail,
                        DisplayName = string.IsNullOrWhiteSpace(profile.Name) ? null : profile.Name,
                        AvatarUrl = string.IsNullOrWhiteSpace(profile.Picture) ? null : profile.Picture,
                        PasswordHash = string.Empty,
                        Role = isAdminEmail ? UserRole.Admin : UserRole.Parent,
                        CreatedAt = DateTime.UtcNow,
                        ExternalProvider = GoogleProviderName,
                        ExternalSubject = profile.Sub,
                        EmailVerified = true,
                    };

                    db.Users.Add(user);
                }
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(profile.Name)) user.DisplayName = profile.Name;
                if (!string.IsNullOrWhiteSpace(profile.Picture)) user.AvatarUrl = profile.Picture;
                if (isAdminEmail) user.Role = UserRole.Admin;
            }

            user.LastAccessAt = DateTime.UtcNow;

            var accessToken = tokenService.CreateAccessToken(user);
            var refreshToken = tokenService.CreateRefreshToken();
            var refreshExpiresDays = int.TryParse(configuration["Jwt:RefreshTokenExpirationDays"], out var refreshDays)
                ? refreshDays
                : 14;

            db.RefreshTokens.Add(new RefreshToken
            {
                UserId = user.Id,
                Token = refreshToken,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(refreshExpiresDays)
            });

            await db.SaveChangesAsync(cancellationToken);

            var expiresIn = int.TryParse(configuration["Jwt:AccessTokenExpirationMinutes"], out var accessMinutes)
                ? accessMinutes * 60
                : 1800;

            var authResponse = new AuthTokenResponse(
                accessToken,
                refreshToken,
                expiresIn,
                new AuthUserResponse(user.Id, user.Email, user.Role.ToString(), user.DisplayName ?? user.Email, user.AvatarUrl));

                if (string.IsNullOrWhiteSpace(jwtSigningKey))
                {
                    logger.LogWarning("Cannot build Google finalize auth code because Jwt:Key is not configured.");
                    return Results.Redirect(BuildFrontendCallbackUrl(frontendCallbackUrl, "google_not_configured", null, returnPath));
                }

            var authCode = CreateSignedAuthCode(authResponse, jwtSigningKey);

            return Results.Redirect(BuildFrontendCallbackUrl(frontendCallbackUrl, null, authCode, returnPath));
            }
            else
            {
                return Results.Redirect(BuildFrontendCallbackUrl(defaultFrontendCallback, "google_invalid_state", null, "/parent"));
            }
        });

        apiV1.MapPost("/auth/google/finalize", (GoogleFinalizeRequest request, IMemoryCache cache, IConfiguration configuration) =>
        {
            if (string.IsNullOrWhiteSpace(request.AuthCode))
            {
                return Results.BadRequest(new { error = "Auth code is required." });
            }

            var jwtSigningKey = configuration["Jwt:Key"];
            if (string.IsNullOrWhiteSpace(jwtSigningKey) && string.Equals(configuration["ASPNETCORE_ENVIRONMENT"], "Development", StringComparison.OrdinalIgnoreCase))
            {
                jwtSigningKey = "dev-super-secret-key-change-in-production-32chars";
            }

            if (!string.IsNullOrWhiteSpace(jwtSigningKey)
                && TryReadSignedAuthCode(request.AuthCode, jwtSigningKey, out var signedResponse)
                && signedResponse is not null)
            {
                return Results.Ok(signedResponse);
            }

            // Backward compatibility for auth codes created by previous in-memory implementation.
            if (cache.TryGetValue<AuthTokenResponse>(GetGoogleUsedAuthCodeCacheKey(request.AuthCode), out var usedResponse)
                && usedResponse is not null)
            {
                return Results.Ok(usedResponse);
            }

            if (!cache.TryGetValue<AuthTokenResponse>(GetGoogleAuthCodeCacheKey(request.AuthCode), out var cachedResponse) || cachedResponse is null)
            {
                return Results.Unauthorized();
            }

            cache.Remove(GetGoogleAuthCodeCacheKey(request.AuthCode));
            cache.Set(GetGoogleUsedAuthCodeCacheKey(request.AuthCode), cachedResponse, TimeSpan.FromMinutes(2));
            return Results.Ok(cachedResponse);
        });

        apiV1.MapGet("/auth/child/google/start", (HttpContext httpContext, IConfiguration configuration) =>
        {
            var clientId = configuration["GoogleAuth:ClientId"];
            var redirectUri = configuration["GoogleAuth:ChildRedirectUri"] ?? configuration["GoogleAuth:RedirectUri"];
            var frontendCallbackUrl = configuration["GoogleAuth:ChildFrontendCallbackUrl"];

            if (string.IsNullOrWhiteSpace(clientId)
                || string.IsNullOrWhiteSpace(redirectUri)
                || string.IsNullOrWhiteSpace(frontendCallbackUrl))
            {
                return Results.Problem(
                    title: "Google auth is not configured.",
                    detail: "Set GoogleAuth:ClientId, GoogleAuth:RedirectUri, and GoogleAuth:FrontendCallbackUrl.",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            var returnPath = httpContext.Request.Query["returnPath"].ToString();
            if (!IsValidReturnPath(returnPath))
            {
                returnPath = "/child";
            }

            var registrationTokenRaw = httpContext.Request.Query["registrationToken"].ToString();
            var registrationToken = Guid.TryParse(registrationTokenRaw, out _) ? registrationTokenRaw : null;

            var jwtSigningKey = configuration["Jwt:Key"];
            if (string.IsNullOrWhiteSpace(jwtSigningKey) && string.Equals(configuration["ASPNETCORE_ENVIRONMENT"], "Development", StringComparison.OrdinalIgnoreCase))
            {
                jwtSigningKey = "dev-super-secret-key-change-in-production-32chars";
            }

            if (string.IsNullOrWhiteSpace(jwtSigningKey))
            {
                return Results.Problem("Jwt:Key is not configured.", statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            var state = CreateSignedStateToken(new GoogleAuthStateContext(frontendCallbackUrl, returnPath, RegistrationToken: registrationToken), jwtSigningKey);

            var authorizationUrl = QueryHelpers.AddQueryString("https://accounts.google.com/o/oauth2/v2/auth", new Dictionary<string, string?>
            {
                ["client_id"] = clientId,
                ["redirect_uri"] = redirectUri,
                ["response_type"] = "code",
                ["scope"] = "openid email profile",
                ["state"] = state,
                ["prompt"] = "select_account",
            });

            return Results.Redirect(authorizationUrl);
        });

        apiV1.MapGet("/auth/child/google/callback", async (
            string? code,
            string? state,
            string? error,
            IMemoryCache cache,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
            AppDbContext db,
            IJwtTokenService tokenService,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) =>
        {
            var logger = loggerFactory.CreateLogger("GoogleAuth");
            var defaultFrontendCallback = configuration["GoogleAuth:ChildFrontendCallbackUrl"];

            if (string.IsNullOrWhiteSpace(defaultFrontendCallback))
            {
                return Results.Problem(
                    title: "Google auth is not configured.",
                    detail: "Set GoogleAuth:ChildFrontendCallbackUrl.",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            if (!string.IsNullOrWhiteSpace(error))
            {
                return Results.Redirect(BuildFrontendCallbackUrl(defaultFrontendCallback, "google_access_denied", null, "/child"));
            }

            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
            {
                return Results.Redirect(BuildFrontendCallbackUrl(defaultFrontendCallback, "google_invalid_callback", null, "/child"));
            }

            var jwtSigningKey = configuration["Jwt:Key"];
            if (string.IsNullOrWhiteSpace(jwtSigningKey) && string.Equals(configuration["ASPNETCORE_ENVIRONMENT"], "Development", StringComparison.OrdinalIgnoreCase))
            {
                jwtSigningKey = "dev-super-secret-key-change-in-production-32chars";
            }

            if (!string.IsNullOrWhiteSpace(jwtSigningKey) && TryReadSignedStateToken(state, jwtSigningKey, out var authState) && authState is not null)
            {
                var frontendCallbackUrl = authState.FrontendCallbackUrl;
                var returnPath = IsValidReturnPath(authState.ReturnPath) ? authState.ReturnPath : "/child";

            var clientId = configuration["GoogleAuth:ClientId"];
            var clientSecret = configuration["GoogleAuth:ClientSecret"];
            var redirectUri = configuration["GoogleAuth:ChildRedirectUri"] ?? configuration["GoogleAuth:RedirectUri"];

            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret) || string.IsNullOrWhiteSpace(redirectUri))
            {
                return Results.Redirect(BuildFrontendCallbackUrl(frontendCallbackUrl, "google_not_configured", null, returnPath));
            }

            var httpClient = httpClientFactory.CreateClient();
            var normalizedCode = code.Replace(" ", "+", StringComparison.Ordinal);

            var tokenResponse = await ExchangeGoogleCodeAsync(httpClient, normalizedCode, clientId, clientSecret, redirectUri, logger, cancellationToken);
            if (tokenResponse is null || string.IsNullOrWhiteSpace(tokenResponse.AccessToken))
            {
                logger.LogWarning("Google token exchange failed for child login.");
                return Results.Redirect(BuildFrontendCallbackUrl(frontendCallbackUrl, "google_exchange_failed", null, returnPath));
            }

            var profile = await GetGoogleProfileAsync(httpClient, tokenResponse.AccessToken, cancellationToken);
            if (profile is null || string.IsNullOrWhiteSpace(profile.Sub) || string.IsNullOrWhiteSpace(profile.Email))
            {
                logger.LogWarning("Google profile fetch failed or missing required claims for child.");
                return Results.Redirect(BuildFrontendCallbackUrl(frontendCallbackUrl, "google_profile_invalid", null, returnPath));
            }

            if (!profile.EmailVerified)
            {
                return Results.Redirect(BuildFrontendCallbackUrl(frontendCallbackUrl, "google_email_not_verified", null, returnPath));
            }

            var normalizedEmail = profile.Email.Trim().ToLowerInvariant();

            // Registration flow: token in state means this is a new child completing registration via Google SSO
            if (!string.IsNullOrWhiteSpace(authState.RegistrationToken))
            {
                var pendingChild = await db.Children
                    .Include(x => x.User)
                    .FirstOrDefaultAsync(x => x.RegistrationToken == authState.RegistrationToken, cancellationToken);

                if (pendingChild is null)
                    return Results.Redirect(BuildFrontendCallbackUrl(frontendCallbackUrl, "registration_token_invalid", null, returnPath));

                if (pendingChild.UserId is not null)
                    return Results.Redirect(BuildFrontendCallbackUrl(frontendCallbackUrl, "child_already_registered", null, returnPath));

                if (!string.Equals(pendingChild.EnrollmentEmail, normalizedEmail, StringComparison.OrdinalIgnoreCase))
                    return Results.Redirect(BuildFrontendCallbackUrl(frontendCallbackUrl, "google_email_mismatch", null, returnPath));

                var newChildUser = new AppUser
                {
                    Email = normalizedEmail,
                    DisplayName = string.IsNullOrWhiteSpace(profile.Name) ? null : profile.Name,
                    AvatarUrl = string.IsNullOrWhiteSpace(profile.Picture) ? null : profile.Picture,
                    PasswordHash = string.Empty,
                    Role = UserRole.Child,
                    CreatedAt = DateTime.UtcNow,
                    LastAccessAt = DateTime.UtcNow,
                    ExternalProvider = GoogleProviderName,
                    ExternalSubject = profile.Sub,
                    EmailVerified = true,
                };
                db.Users.Add(newChildUser);
                pendingChild.UserId = newChildUser.Id;
                pendingChild.User = newChildUser;

                var regRefreshDays = int.TryParse(configuration["Jwt:RefreshTokenExpirationDays"], out var rrd) ? rrd : 14;
                var regRefreshToken = tokenService.CreateRefreshToken();
                db.RefreshTokens.Add(new RefreshToken
                {
                    UserId = newChildUser.Id,
                    Token = regRefreshToken,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddDays(regRefreshDays),
                });

                await db.SaveChangesAsync(cancellationToken);

                var regAccessToken = tokenService.CreateAccessToken(newChildUser);
                var regExpiresIn = int.TryParse(configuration["Jwt:AccessTokenExpirationMinutes"], out var ram) ? ram * 60 : 1800;
                var regAuthResponse = new AuthTokenResponse(regAccessToken, regRefreshToken, regExpiresIn,
                    new AuthUserResponse(newChildUser.Id, newChildUser.Email, newChildUser.Role.ToString(), pendingChild.Name, newChildUser.AvatarUrl));
                var regAuthCode = CreateSignedAuthCode(regAuthResponse, jwtSigningKey);
                return Results.Redirect(BuildFrontendCallbackUrl(frontendCallbackUrl, null, regAuthCode, returnPath));
            }

            // Check if this email is a parent's email - reject child login attempt
            var parentUser = await db.Users.AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.Email == normalizedEmail && x.Role == UserRole.Parent,
                    cancellationToken);

            if (parentUser is not null)
            {
                logger.LogWarning("Attempted child login with parent email: {Email}", normalizedEmail);
                return Results.Redirect(BuildFrontendCallbackUrl(frontendCallbackUrl, "child_not_registered", null, returnPath));
            }

            var childUser = await db.Users.AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.ExternalProvider == GoogleProviderName && x.ExternalSubject == profile.Sub && x.Role == UserRole.Child,
                    cancellationToken);

            if (childUser is null)
            {
                childUser = await db.Users.AsNoTracking()
                    .FirstOrDefaultAsync(
                        x => x.Email == normalizedEmail && x.Role == UserRole.Child,
                        cancellationToken);
            }

            if (childUser is null)
            {
                logger.LogWarning("Child login attempted with unregistered email: {Email}", normalizedEmail);
                return Results.Redirect(BuildFrontendCallbackUrl(frontendCallbackUrl, "child_not_registered", null, returnPath));
            }

            var child = await db.Children
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserId == childUser.Id, cancellationToken);

            if (child is null || child.ParentId == Guid.Empty)
            {
                logger.LogWarning("Child login: Child record not found or parent not assigned for UserId: {UserId}", childUser.Id);
                return Results.Redirect(BuildFrontendCallbackUrl(frontendCallbackUrl, "child_not_registered", null, returnPath));
            }

            if (childUser.ExternalProvider != GoogleProviderName || childUser.ExternalSubject != profile.Sub
                || (!string.IsNullOrWhiteSpace(profile.Picture) && childUser.AvatarUrl != profile.Picture))
            {
                childUser.ExternalProvider = GoogleProviderName;
                childUser.ExternalSubject = profile.Sub;
                childUser.EmailVerified = true;
                if (!string.IsNullOrWhiteSpace(profile.Picture)) childUser.AvatarUrl = profile.Picture;
                db.Users.Update(childUser);
                await db.SaveChangesAsync(cancellationToken);
            }

            var accessToken = tokenService.CreateAccessToken(childUser);
            var refreshToken = tokenService.CreateRefreshToken();
            var refreshExpiresDays = int.TryParse(configuration["Jwt:RefreshTokenExpirationDays"], out var refreshDays)
                ? refreshDays
                : 14;

            db.RefreshTokens.Add(new RefreshToken
            {
                UserId = childUser.Id,
                Token = refreshToken,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(refreshExpiresDays)
            });

            await db.SaveChangesAsync(cancellationToken);

            var expiresIn = int.TryParse(configuration["Jwt:AccessTokenExpirationMinutes"], out var accessMinutes)
                ? accessMinutes * 60
                : 1800;

            var authResponse = new AuthTokenResponse(
                accessToken,
                refreshToken,
                expiresIn,
                new AuthUserResponse(childUser.Id, childUser.Email, childUser.Role.ToString(), child.Name, childUser.AvatarUrl));

                if (string.IsNullOrWhiteSpace(jwtSigningKey))
                {
                    logger.LogWarning("Cannot build child Google finalize auth code because Jwt:Key is not configured.");
                    return Results.Redirect(BuildFrontendCallbackUrl(frontendCallbackUrl, "google_not_configured", null, returnPath));
                }

                var authCode = CreateSignedAuthCode(authResponse, jwtSigningKey);

                return Results.Redirect(BuildFrontendCallbackUrl(frontendCallbackUrl, null, authCode, returnPath));
            }
            else
            {
                return Results.Redirect(BuildFrontendCallbackUrl(defaultFrontendCallback, "google_invalid_state", null, "/child"));
            }
        });

        apiV1.MapPost("/auth/child/google/finalize", (ChildGoogleFinalizeRequest request, IMemoryCache cache, IConfiguration configuration) =>
        {
            if (string.IsNullOrWhiteSpace(request.AuthCode))
            {
                return Results.BadRequest(new { error = "Auth code is required." });
            }

            var jwtSigningKey = configuration["Jwt:Key"];
            if (string.IsNullOrWhiteSpace(jwtSigningKey) && string.Equals(configuration["ASPNETCORE_ENVIRONMENT"], "Development", StringComparison.OrdinalIgnoreCase))
            {
                jwtSigningKey = "dev-super-secret-key-change-in-production-32chars";
            }

            if (!string.IsNullOrWhiteSpace(jwtSigningKey)
                && TryReadSignedAuthCode(request.AuthCode, jwtSigningKey, out var signedResponse)
                && signedResponse is not null)
            {
                return Results.Ok(signedResponse);
            }

            return Results.Unauthorized();
        });

        apiV1.MapGet("/auth/google/unified/start", (HttpContext httpContext, IConfiguration configuration) =>
        {
            var clientId = configuration["GoogleAuth:ClientId"];
            // Reuse the parent redirect URI — no extra Google Console entry needed
            var redirectUri = configuration["GoogleAuth:RedirectUri"];
            var frontendCallbackUrl = configuration["GoogleAuth:UnifiedFrontendCallbackUrl"] ?? configuration["GoogleAuth:ParentFrontendCallbackUrl"];

            if (string.IsNullOrWhiteSpace(clientId)
                || string.IsNullOrWhiteSpace(redirectUri)
                || string.IsNullOrWhiteSpace(frontendCallbackUrl))
            {
                return Results.Problem(
                    title: "Google auth is not configured.",
                    detail: "Set GoogleAuth:ClientId, GoogleAuth:RedirectUri, and GoogleAuth:UnifiedFrontendCallbackUrl.",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            var returnPath = httpContext.Request.Query["returnPath"].ToString();
            if (!IsValidReturnPath(returnPath))
            {
                returnPath = "/parent";
            }

            var jwtSigningKey = configuration["Jwt:Key"];
            if (string.IsNullOrWhiteSpace(jwtSigningKey) && string.Equals(configuration["ASPNETCORE_ENVIRONMENT"], "Development", StringComparison.OrdinalIgnoreCase))
            {
                jwtSigningKey = "dev-super-secret-key-change-in-production-32chars";
            }

            if (string.IsNullOrWhiteSpace(jwtSigningKey))
            {
                return Results.Problem("Jwt:Key is not configured.", statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            var state = CreateSignedStateToken(new GoogleAuthStateContext(frontendCallbackUrl, returnPath, IsUnified: true), jwtSigningKey);

            var authorizationUrl = QueryHelpers.AddQueryString("https://accounts.google.com/o/oauth2/v2/auth", new Dictionary<string, string?>
            {
                ["client_id"] = clientId,
                ["redirect_uri"] = redirectUri,
                ["response_type"] = "code",
                ["scope"] = "openid email profile",
                ["state"] = state,
                ["prompt"] = "select_account",
            });

            return Results.Redirect(authorizationUrl);
        });

        apiV1.MapPost("/auth/google/unified/finalize", (GoogleFinalizeRequest request, IMemoryCache cache, IConfiguration configuration) =>
        {
            if (string.IsNullOrWhiteSpace(request.AuthCode))
            {
                return Results.BadRequest(new { error = "Auth code is required." });
            }

            var jwtSigningKey = configuration["Jwt:Key"];
            if (string.IsNullOrWhiteSpace(jwtSigningKey) && string.Equals(configuration["ASPNETCORE_ENVIRONMENT"], "Development", StringComparison.OrdinalIgnoreCase))
            {
                jwtSigningKey = "dev-super-secret-key-change-in-production-32chars";
            }

            if (!string.IsNullOrWhiteSpace(jwtSigningKey)
                && TryReadSignedAuthCode(request.AuthCode, jwtSigningKey, out var signedResponse)
                && signedResponse is not null)
            {
                return Results.Ok(signedResponse);
            }

            return Results.Unauthorized();
        });

        return apiV1;
    }

    private static bool IsValidReturnPath(string? path)
    {
        return !string.IsNullOrWhiteSpace(path)
            && path.StartsWith('/')
            && !path.StartsWith("//");
    }

    private static string BuildFrontendCallbackUrl(string baseCallbackUrl, string? error, string? authCode, string returnPath)
    {
        var query = new Dictionary<string, string?>
        {
            ["returnPath"] = IsValidReturnPath(returnPath) ? returnPath : "/parent"
        };

        if (!string.IsNullOrWhiteSpace(error))
        {
            query["error"] = error;
        }

        if (!string.IsNullOrWhiteSpace(authCode))
        {
            query["authCode"] = authCode;
        }

        return QueryHelpers.AddQueryString(baseCallbackUrl, query);
    }

    private static string GetGoogleAuthCodeCacheKey(string authCode)
        => $"google-auth-code:{authCode}";

    private static string GetGoogleUsedAuthCodeCacheKey(string authCode)
        => $"google-auth-code-used:{authCode}";

    private static string CreateSecureToken()
        => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    private static string CreateSignedStateToken(GoogleAuthStateContext context, string signingKey)
    {
        var payload = new GoogleAuthStatePayload(DateTime.UtcNow, context);
        var payloadJson = JsonSerializer.Serialize(payload);
        var payloadBytes = Encoding.UTF8.GetBytes(payloadJson);
        var payloadToken = WebEncoders.Base64UrlEncode(payloadBytes);

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(signingKey));
        var signatureBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(payloadToken));
        var signatureToken = WebEncoders.Base64UrlEncode(signatureBytes);

        return $"{payloadToken}.{signatureToken}";
    }

    private static string CreateSignedAuthCode(AuthTokenResponse response, string signingKey)
    {
        var payload = new GoogleAuthCodePayload(DateTime.UtcNow, response);
        var payloadJson = JsonSerializer.Serialize(payload);
        var payloadBytes = Encoding.UTF8.GetBytes(payloadJson);
        var payloadToken = WebEncoders.Base64UrlEncode(payloadBytes);

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(signingKey));
        var signatureBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(payloadToken));
        var signatureToken = WebEncoders.Base64UrlEncode(signatureBytes);

        return $"{payloadToken}.{signatureToken}";
    }

    private static bool TryReadSignedStateToken(string stateToken, string signingKey, out GoogleAuthStateContext? context)
    {
        context = null;

        var parts = stateToken.Split('.', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            return false;
        }

        var payloadToken = parts[0];
        var providedSignatureToken = parts[1];

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(signingKey));
        var expectedSignature = hmac.ComputeHash(Encoding.UTF8.GetBytes(payloadToken));

        byte[] providedSignature;
        try
        {
            providedSignature = WebEncoders.Base64UrlDecode(providedSignatureToken);
        }
        catch
        {
            return false;
        }

        if (!CryptographicOperations.FixedTimeEquals(expectedSignature, providedSignature))
        {
            return false;
        }

        GoogleAuthStatePayload? payload;
        try
        {
            var payloadBytes = WebEncoders.Base64UrlDecode(payloadToken);
            payload = JsonSerializer.Deserialize<GoogleAuthStatePayload>(payloadBytes);
        }
        catch
        {
            return false;
        }

        if (payload is null)
        {
            return false;
        }

        const int StateTokenTtlMinutes = 10;
        if (payload.IssuedAtUtc.AddMinutes(StateTokenTtlMinutes) < DateTime.UtcNow)
        {
            return false;
        }

        context = payload.Context;
        return context is not null;
    }

    private static bool TryReadSignedAuthCode(string authCode, string signingKey, out AuthTokenResponse? response)
    {
        response = null;

        var parts = authCode.Split('.', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            return false;
        }

        var payloadToken = parts[0];
        var providedSignatureToken = parts[1];

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(signingKey));
        var expectedSignature = hmac.ComputeHash(Encoding.UTF8.GetBytes(payloadToken));

        byte[] providedSignature;
        try
        {
            providedSignature = WebEncoders.Base64UrlDecode(providedSignatureToken);
        }
        catch
        {
            return false;
        }

        if (!CryptographicOperations.FixedTimeEquals(expectedSignature, providedSignature))
        {
            return false;
        }

        GoogleAuthCodePayload? payload;
        try
        {
            var payloadBytes = WebEncoders.Base64UrlDecode(payloadToken);
            payload = JsonSerializer.Deserialize<GoogleAuthCodePayload>(payloadBytes);
        }
        catch
        {
            return false;
        }

        if (payload is null)
        {
            return false;
        }

        if (payload.IssuedAtUtc.AddMinutes(GoogleAuthCodeTtlMinutes) < DateTime.UtcNow)
        {
            return false;
        }

        response = payload.Response;
        return response is not null;
    }

    private static async Task<GoogleTokenResponse?> ExchangeGoogleCodeAsync(
        HttpClient httpClient,
        string code,
        string clientId,
        string clientSecret,
        string redirectUri,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"] = code,
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["redirect_uri"] = redirectUri,
            ["grant_type"] = "authorization_code",
        });

        var response = await httpClient.PostAsync("https://oauth2.googleapis.com/token", content, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning("Google token endpoint failed with status {StatusCode}. Body: {Body}", (int)response.StatusCode, errorBody);
            return null;
        }

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<GoogleTokenResponse>(stream, cancellationToken: cancellationToken);
    }

    private static async Task<GoogleUserProfile?> GetGoogleProfileAsync(
        HttpClient httpClient,
        string accessToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://openidconnect.googleapis.com/v1/userinfo");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<GoogleUserProfile>(stream, cancellationToken: cancellationToken);
    }

    private sealed record GoogleAuthStateContext(string FrontendCallbackUrl, string ReturnPath, bool IsUnified = false, string? RegistrationToken = null);

    private sealed record GoogleAuthStatePayload(DateTime IssuedAtUtc, GoogleAuthStateContext Context);

    private sealed record GoogleAuthCodePayload(DateTime IssuedAtUtc, AuthTokenResponse Response);

    private sealed record GoogleTokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken);

    private sealed record GoogleUserProfile(
        [property: JsonPropertyName("sub")] string Sub,
        [property: JsonPropertyName("email")] string Email,
        [property: JsonPropertyName("email_verified")] bool EmailVerified,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("picture")] string? Picture);
}
