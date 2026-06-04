using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

// CORS — allow React dev server + production frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        var origins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()
                      ?? new[] { "http://localhost:5173" };
        policy.WithOrigins(origins)
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// PostgreSQL via EF Core
var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? builder.Configuration.GetConnectionString("Postgres");

if (string.IsNullOrWhiteSpace(connectionString))
{
    if (builder.Environment.IsDevelopment())
    {
        connectionString = "Host=localhost;Database=kidslearn;Username=postgres;Password=postgres";
    }
    else
    {
        throw new InvalidOperationException(
            "Missing database connection string. Set ConnectionStrings__Postgres or DATABASE_URL.");
    }
}

connectionString = PostgresConnectionStringHelper.Normalize(connectionString);

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(connectionString));

builder.Services.AddScoped<IPasswordHasherService, PasswordHasherService>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();

var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey) && builder.Environment.IsDevelopment())
{
    jwtKey = "dev-super-secret-key-change-in-production-32chars";
}

if (string.IsNullOrWhiteSpace(jwtKey))
{
    throw new InvalidOperationException("Jwt:Key is not configured.");
}

var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "KidsLearn.Api";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "KidsLearn.Client";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ParentOnly", policy => policy.RequireRole(UserRole.Parent.ToString()));
    options.AddPolicy("ChildOnly", policy => policy.RequireRole(UserRole.Child.ToString()));
    options.AddPolicy("AdminOnly", policy => policy.RequireRole(UserRole.Admin.ToString()));
});

var app = builder.Build();

// Auto-migrate on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    // If there are no migration files, create schema directly.
    if (db.Database.GetMigrations().Any())
    {
        db.Database.Migrate();
    }
    else
    {
        db.Database.EnsureCreated();
    }
}

app.UseCors("AllowFrontend");
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

var apiV1 = app.MapGroup("/api/v1");
apiV1.MapGet("/health", () => Results.Ok(new { status = "healthy", version = "v1" }));

static Guid? ResolveUserId(ClaimsPrincipal user)
{
    var candidate = user.FindFirstValue("sub")
                    ?? user.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub);

    return Guid.TryParse(candidate, out var userId) ? userId : null;
}

apiV1.MapPost("/auth/register", async (AppDbContext db, IPasswordHasherService passwordHasher, RegisterRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
    {
        return Results.BadRequest(new { error = "Email and password are required." });
    }

    var email = request.Email.Trim().ToLowerInvariant();
    if (!email.Contains('@'))
    {
        return Results.BadRequest(new { error = "Email format is invalid." });
    }

    if (request.Password.Length < 8)
    {
        return Results.BadRequest(new { error = "Password must be at least 8 characters." });
    }

    var exists = await db.Users.AnyAsync(x => x.Email == email);
    if (exists)
    {
        return Results.Conflict(new { error = "User with this email already exists." });
    }

    var user = new AppUser
    {
        Email = email,
        PasswordHash = passwordHasher.HashPassword(request.Password),
        Role = UserRole.Parent,
        CreatedAt = DateTime.UtcNow
    };

    db.Users.Add(user);
    await db.SaveChangesAsync();

    return Results.Created($"/api/v1/users/{user.Id}", new AuthUserResponse(user.Id, user.Email, user.Role.ToString()));
});

apiV1.MapPost("/auth/login", async (AppDbContext db, IPasswordHasherService passwordHasher, IJwtTokenService tokenService, LoginRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
    {
        return Results.BadRequest(new { error = "Email and password are required." });
    }

    var email = request.Email.Trim().ToLowerInvariant();
    var user = await db.Users.FirstOrDefaultAsync(x => x.Email == email);
    if (user is null || string.IsNullOrWhiteSpace(user.PasswordHash) || !passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
    {
        return Results.Unauthorized();
    }

    var accessToken = tokenService.CreateAccessToken(user);
    var refreshToken = tokenService.CreateRefreshToken();
    var refreshExpiresDays = int.TryParse(builder.Configuration["Jwt:RefreshTokenExpirationDays"], out var refreshDays)
        ? refreshDays
        : 14;

    db.RefreshTokens.Add(new RefreshToken
    {
        UserId = user.Id,
        Token = refreshToken,
        CreatedAt = DateTime.UtcNow,
        ExpiresAt = DateTime.UtcNow.AddDays(refreshExpiresDays)
    });

    await db.SaveChangesAsync();

    var expiresIn = int.TryParse(builder.Configuration["Jwt:AccessTokenExpirationMinutes"], out var accessMinutes)
        ? accessMinutes * 60
        : 1800;

    return Results.Ok(new AuthTokenResponse(
        accessToken,
        refreshToken,
        expiresIn,
        new AuthUserResponse(user.Id, user.Email, user.Role.ToString())));
});

apiV1.MapPost("/auth/refresh", async (AppDbContext db, IJwtTokenService tokenService, RefreshRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.RefreshToken))
    {
        return Results.BadRequest(new { error = "Refresh token is required." });
    }

    var existing = await db.RefreshTokens
        .Include(x => x.User)
        .FirstOrDefaultAsync(x => x.Token == request.RefreshToken);

    if (existing is null || existing.RevokedAt.HasValue || existing.ExpiresAt <= DateTime.UtcNow)
    {
        return Results.Unauthorized();
    }

    existing.RevokedAt = DateTime.UtcNow;

    var newRefreshToken = tokenService.CreateRefreshToken();
    var refreshExpiresDays = int.TryParse(builder.Configuration["Jwt:RefreshTokenExpirationDays"], out var refreshDays)
        ? refreshDays
        : 14;

    db.RefreshTokens.Add(new RefreshToken
    {
        UserId = existing.UserId,
        Token = newRefreshToken,
        CreatedAt = DateTime.UtcNow,
        ExpiresAt = DateTime.UtcNow.AddDays(refreshExpiresDays)
    });

    var accessToken = tokenService.CreateAccessToken(existing.User);
    await db.SaveChangesAsync();

    var expiresIn = int.TryParse(builder.Configuration["Jwt:AccessTokenExpirationMinutes"], out var accessMinutes)
        ? accessMinutes * 60
        : 1800;

    return Results.Ok(new AuthTokenResponse(
        accessToken,
        newRefreshToken,
        expiresIn,
        new AuthUserResponse(existing.User.Id, existing.User.Email, existing.User.Role.ToString())));
});

apiV1.MapPost("/auth/revoke", async (AppDbContext db, RevokeRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.RefreshToken))
    {
        return Results.BadRequest(new { error = "Refresh token is required." });
    }

    var existing = await db.RefreshTokens.FirstOrDefaultAsync(x => x.Token == request.RefreshToken);
    if (existing is null)
    {
        return Results.NotFound();
    }

    if (!existing.RevokedAt.HasValue)
    {
        existing.RevokedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    return Results.NoContent();
});

var parentApi = apiV1.MapGroup("")
    .RequireAuthorization("ParentOnly");

parentApi.MapGet("/children", async (AppDbContext db, ClaimsPrincipal user) =>
{
    var parentId = ResolveUserId(user);
    if (!parentId.HasValue)
    {
        return Results.Unauthorized();
    }

    var children = await db.Children
        .Where(x => x.ParentId == parentId.Value)
        .Select(x => new ChildResponse(x.Id, x.ParentId, x.Name, x.Grade))
        .ToListAsync();

    return Results.Ok(children);
});

parentApi.MapPost("/children", async (AppDbContext db, ClaimsPrincipal user, CreateChildRequest request) =>
{
    var parentId = ResolveUserId(user);
    if (!parentId.HasValue)
    {
        return Results.Unauthorized();
    }

    if (string.IsNullOrWhiteSpace(request.Name))
    {
        return Results.BadRequest(new { error = "Name is required." });
    }

    if (request.Grade is < 1 or > 12)
    {
        return Results.BadRequest(new { error = "Grade must be between 1 and 12." });
    }

    var parentExists = await db.Users.AnyAsync(x => x.Id == parentId.Value && x.Role == UserRole.Parent);
    if (!parentExists)
    {
        return Results.NotFound(new { error = "Parent was not found." });
    }

    var child = new Child
    {
        ParentId = parentId.Value,
        Name = request.Name.Trim(),
        Grade = request.Grade
    };

    db.Children.Add(child);
    await db.SaveChangesAsync();

    return Results.Created($"/api/v1/children/{child.Id}", new ChildResponse(child.Id, child.ParentId, child.Name, child.Grade));
});

parentApi.MapPatch("/children/{childId:guid}", async (AppDbContext db, ClaimsPrincipal user, Guid childId, UpdateChildRequest request) =>
{
    var parentId = ResolveUserId(user);
    if (!parentId.HasValue)
    {
        return Results.Unauthorized();
    }

    var child = await db.Children.FirstOrDefaultAsync(x => x.Id == childId && x.ParentId == parentId.Value);
    if (child is null)
    {
        return Results.NotFound();
    }

    if (request.Name is not null)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Results.BadRequest(new { error = "Name cannot be empty." });
        }

        child.Name = request.Name.Trim();
    }

    if (request.Grade.HasValue)
    {
        if (request.Grade.Value is < 1 or > 12)
        {
            return Results.BadRequest(new { error = "Grade must be between 1 and 12." });
        }

        child.Grade = request.Grade.Value;
    }

    await db.SaveChangesAsync();
    return Results.Ok(new ChildResponse(child.Id, child.ParentId, child.Name, child.Grade));
});

parentApi.MapDelete("/children/{childId:guid}", async (AppDbContext db, ClaimsPrincipal user, Guid childId) =>
{
    var parentId = ResolveUserId(user);
    if (!parentId.HasValue)
    {
        return Results.Unauthorized();
    }

    var child = await db.Children.FirstOrDefaultAsync(x => x.Id == childId && x.ParentId == parentId.Value);
    if (child is null)
    {
        return Results.NotFound();
    }

    db.Children.Remove(child);
    await db.SaveChangesAsync();

    return Results.NoContent();
});

// Main endpoint — returns greeting from DB
app.MapGet("/api/hello", async (AppDbContext db) =>
{
    var greeting = await db.Greetings.OrderByDescending(g => g.Id).FirstOrDefaultAsync();
    return Results.Ok(new
    {
        message = greeting?.Text ?? "Hello, World!",
        timestamp = DateTime.UtcNow,
        source = "PostgreSQL"
    });
});

// Seed a custom greeting
app.MapPost("/api/hello", async (AppDbContext db, GreetingRequest req) =>
{
    var greeting = new Greeting { Text = req.Message, CreatedAt = DateTime.UtcNow };
    db.Greetings.Add(greeting);
    await db.SaveChangesAsync();
    return Results.Created($"/api/hello", greeting);
});

app.MapFallbackToFile("index.html");

app.Run();
