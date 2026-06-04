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

static string GenerateAccessCode()
{
    return Random.Shared.Next(100000, 999999).ToString();
}

static async Task<Guid?> ResolveChildIdAsync(AppDbContext db, ClaimsPrincipal user)
{
    var userId = ResolveUserId(user);
    if (!userId.HasValue)
    {
        return null;
    }

    return await db.Children
        .AsNoTracking()
        .Where(x => x.UserId == userId.Value)
        .Select(x => (Guid?)x.Id)
        .FirstOrDefaultAsync();
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

apiV1.MapPost("/auth/child-login", async (AppDbContext db, IPasswordHasherService passwordHasher, IJwtTokenService tokenService, ChildLoginRequest request) =>
{
    if (request.ChildId == Guid.Empty || string.IsNullOrWhiteSpace(request.AccessCode))
    {
        return Results.BadRequest(new { error = "ChildId and access code are required." });
    }

    var child = await db.Children
        .Include(x => x.User)
        .FirstOrDefaultAsync(x => x.Id == request.ChildId);

    if (child?.User is null || child.User.Role != UserRole.Child)
    {
        return Results.Unauthorized();
    }

    if (string.IsNullOrWhiteSpace(child.User.PasswordHash)
        || !passwordHasher.VerifyPassword(request.AccessCode.Trim(), child.User.PasswordHash))
    {
        return Results.Unauthorized();
    }

    var accessToken = tokenService.CreateAccessToken(child.User);
    var refreshToken = tokenService.CreateRefreshToken();
    var refreshExpiresDays = int.TryParse(builder.Configuration["Jwt:RefreshTokenExpirationDays"], out var refreshDays)
        ? refreshDays
        : 14;

    db.RefreshTokens.Add(new RefreshToken
    {
        UserId = child.User.Id,
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
        new AuthUserResponse(child.User.Id, child.Name, child.User.Role.ToString())));
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

parentApi.MapPost("/children", async (AppDbContext db, ClaimsPrincipal user, IPasswordHasherService passwordHasher, CreateChildRequest request) =>
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

    var accessCode = string.IsNullOrWhiteSpace(request.AccessCode)
        ? GenerateAccessCode()
        : request.AccessCode.Trim();

    if (accessCode.Length < 4)
    {
        return Results.BadRequest(new { error = "Access code must contain at least 4 characters." });
    }

    var childUser = new AppUser
    {
        Email = $"child-{Guid.NewGuid():N}@kidslearn.local",
        PasswordHash = passwordHasher.HashPassword(accessCode),
        Role = UserRole.Child,
        CreatedAt = DateTime.UtcNow
    };

    var child = new Child
    {
        ParentId = parentId.Value,
        User = childUser,
        Name = request.Name.Trim(),
        Grade = request.Grade
    };

    db.Children.Add(child);
    await db.SaveChangesAsync();

    return Results.Created(
        $"/api/v1/children/{child.Id}",
        new CreatedChildResponse(new ChildResponse(child.Id, child.ParentId, child.Name, child.Grade), accessCode));
});

parentApi.MapPatch("/children/{childId:guid}", async (AppDbContext db, ClaimsPrincipal user, IPasswordHasherService passwordHasher, Guid childId, UpdateChildRequest request) =>
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

    if (request.AccessCode is not null)
    {
        if (string.IsNullOrWhiteSpace(request.AccessCode) || request.AccessCode.Trim().Length < 4)
        {
            return Results.BadRequest(new { error = "Access code must contain at least 4 characters." });
        }

        if (child.UserId.HasValue)
        {
            var childUser = await db.Users.FirstOrDefaultAsync(x => x.Id == child.UserId.Value && x.Role == UserRole.Child);
            if (childUser is not null)
            {
                childUser.PasswordHash = passwordHasher.HashPassword(request.AccessCode.Trim());
            }
        }
    }

    await db.SaveChangesAsync();
    return Results.Ok(new ChildResponse(child.Id, child.ParentId, child.Name, child.Grade));
});

parentApi.MapPost("/children/{childId:guid}/access-code/reset", async (AppDbContext db, ClaimsPrincipal user, IPasswordHasherService passwordHasher, Guid childId) =>
{
    var parentId = ResolveUserId(user);
    if (!parentId.HasValue)
    {
        return Results.Unauthorized();
    }

    var child = await db.Children.FirstOrDefaultAsync(x => x.Id == childId && x.ParentId == parentId.Value);
    if (child is null || !child.UserId.HasValue)
    {
        return Results.NotFound();
    }

    var childUser = await db.Users.FirstOrDefaultAsync(x => x.Id == child.UserId.Value && x.Role == UserRole.Child);
    if (childUser is null)
    {
        return Results.NotFound();
    }

    var newCode = GenerateAccessCode();
    childUser.PasswordHash = passwordHasher.HashPassword(newCode);
    await db.SaveChangesAsync();

    return Results.Ok(new ResetChildAccessCodeResponse(child.Id, newCode));
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

    AppUser? childUser = null;
    if (child.UserId.HasValue)
    {
        childUser = await db.Users.FirstOrDefaultAsync(x => x.Id == child.UserId.Value && x.Role == UserRole.Child);
    }

    db.Children.Remove(child);
    if (childUser is not null)
    {
        db.Users.Remove(childUser);
    }

    await db.SaveChangesAsync();

    return Results.NoContent();
});

parentApi.MapPost("/lessons", async (AppDbContext db, ClaimsPrincipal user, CreateLessonRequest request) =>
{
    var parentId = ResolveUserId(user);
    if (!parentId.HasValue)
    {
        return Results.Unauthorized();
    }

    if (string.IsNullOrWhiteSpace(request.Title)
        || string.IsNullOrWhiteSpace(request.Subject)
        || string.IsNullOrWhiteSpace(request.Topic)
        || request.Grade is < 1 or > 12)
    {
        return Results.BadRequest(new { error = "Title, subject, topic and grade (1-12) are required." });
    }

    if (request.Questions is null || request.Questions.Count == 0)
    {
        return Results.BadRequest(new { error = "At least one question is required." });
    }

    var lesson = new Lesson
    {
        Title = request.Title.Trim(),
        Subject = request.Subject.Trim(),
        Grade = request.Grade,
        Topic = request.Topic.Trim(),
        Difficulty = string.IsNullOrWhiteSpace(request.Difficulty) ? "Medium" : request.Difficulty.Trim(),
        CreatedBy = parentId.Value,
        CreatedAt = DateTime.UtcNow
    };

    for (var i = 0; i < request.Questions.Count; i++)
    {
        var sourceQuestion = request.Questions[i];
        if (string.IsNullOrWhiteSpace(sourceQuestion.QuestionText)
            || sourceQuestion.Answers is null
            || sourceQuestion.Answers.Count < 2)
        {
            return Results.BadRequest(new { error = "Each question must have text and at least two answers." });
        }

        if (!sourceQuestion.Answers.Any(x => x.IsCorrect))
        {
            return Results.BadRequest(new { error = "Each question must include at least one correct answer." });
        }

        var question = new Question
        {
            QuestionText = sourceQuestion.QuestionText.Trim(),
            Explanation = sourceQuestion.Explanation?.Trim() ?? string.Empty,
            Order = sourceQuestion.Order ?? (i + 1)
        };

        for (var answerIndex = 0; answerIndex < sourceQuestion.Answers.Count; answerIndex++)
        {
            var sourceAnswer = sourceQuestion.Answers[answerIndex];
            if (string.IsNullOrWhiteSpace(sourceAnswer.AnswerText))
            {
                return Results.BadRequest(new { error = "Answer text is required." });
            }

            question.Answers.Add(new AnswerOption
            {
                AnswerText = sourceAnswer.AnswerText.Trim(),
                IsCorrect = sourceAnswer.IsCorrect,
                Order = sourceAnswer.Order ?? (answerIndex + 1)
            });
        }

        lesson.Questions.Add(question);
    }

    db.Lessons.Add(lesson);
    await db.SaveChangesAsync();

    var response = new LessonSummaryResponse(
        lesson.Id,
        lesson.Title,
        lesson.Subject,
        lesson.Grade,
        lesson.Topic,
        lesson.Difficulty,
        lesson.CreatedAt,
        lesson.Questions.Count);

    return Results.Created($"/api/v1/lessons/{lesson.Id}", response);
});

parentApi.MapGet("/lessons", async (AppDbContext db, ClaimsPrincipal user, string? subject, int? grade, string? topic, int page = 1, int pageSize = 20) =>
{
    var parentId = ResolveUserId(user);
    if (!parentId.HasValue)
    {
        return Results.Unauthorized();
    }

    page = Math.Max(page, 1);
    pageSize = Math.Clamp(pageSize, 1, 100);

    var query = db.Lessons
        .AsNoTracking()
        .Where(x => x.CreatedBy == parentId.Value);

    if (!string.IsNullOrWhiteSpace(subject))
    {
        query = query.Where(x => x.Subject == subject.Trim());
    }

    if (grade.HasValue)
    {
        query = query.Where(x => x.Grade == grade.Value);
    }

    if (!string.IsNullOrWhiteSpace(topic))
    {
        query = query.Where(x => x.Topic.Contains(topic.Trim()));
    }

    var total = await query.CountAsync();
    var items = await query
        .OrderByDescending(x => x.CreatedAt)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .Select(x => new LessonSummaryResponse(
            x.Id,
            x.Title,
            x.Subject,
            x.Grade,
            x.Topic,
            x.Difficulty,
            x.CreatedAt,
            x.Questions.Count))
        .ToListAsync();

    return Results.Ok(new { items, total, page, pageSize });
});

parentApi.MapGet("/lessons/{lessonId:guid}", async (AppDbContext db, ClaimsPrincipal user, Guid lessonId) =>
{
    var parentId = ResolveUserId(user);
    if (!parentId.HasValue)
    {
        return Results.Unauthorized();
    }

    var lesson = await db.Lessons
        .AsNoTracking()
        .Include(x => x.Questions.OrderBy(q => q.Order))
        .ThenInclude(q => q.Answers.OrderBy(a => a.Order))
        .FirstOrDefaultAsync(x => x.Id == lessonId && x.CreatedBy == parentId.Value);

    if (lesson is null)
    {
        return Results.NotFound();
    }

    var response = new LessonDetailResponse(
        lesson.Id,
        lesson.Title,
        lesson.Subject,
        lesson.Grade,
        lesson.Topic,
        lesson.Difficulty,
        lesson.CreatedAt,
        lesson.Questions
            .OrderBy(q => q.Order)
            .Select(q => new QuestionResponse(
                q.Id,
                q.QuestionText,
                q.Explanation,
                q.Order,
                q.Answers
                    .OrderBy(a => a.Order)
                    .Select(a => new AnswerOptionResponse(a.Id, a.AnswerText, a.IsCorrect, a.Order))
                    .ToList()))
            .ToList());

    return Results.Ok(response);
});

parentApi.MapPatch("/lessons/{lessonId:guid}", async (AppDbContext db, ClaimsPrincipal user, Guid lessonId, UpdateLessonRequest request) =>
{
    var parentId = ResolveUserId(user);
    if (!parentId.HasValue)
    {
        return Results.Unauthorized();
    }

    var lesson = await db.Lessons.FirstOrDefaultAsync(x => x.Id == lessonId && x.CreatedBy == parentId.Value);
    if (lesson is null)
    {
        return Results.NotFound();
    }

    if (request.Title is not null)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return Results.BadRequest(new { error = "Title cannot be empty." });
        }

        lesson.Title = request.Title.Trim();
    }

    if (request.Subject is not null)
    {
        if (string.IsNullOrWhiteSpace(request.Subject))
        {
            return Results.BadRequest(new { error = "Subject cannot be empty." });
        }

        lesson.Subject = request.Subject.Trim();
    }

    if (request.Topic is not null)
    {
        if (string.IsNullOrWhiteSpace(request.Topic))
        {
            return Results.BadRequest(new { error = "Topic cannot be empty." });
        }

        lesson.Topic = request.Topic.Trim();
    }

    if (request.Difficulty is not null)
    {
        if (string.IsNullOrWhiteSpace(request.Difficulty))
        {
            return Results.BadRequest(new { error = "Difficulty cannot be empty." });
        }

        lesson.Difficulty = request.Difficulty.Trim();
    }

    if (request.Grade.HasValue)
    {
        if (request.Grade.Value is < 1 or > 12)
        {
            return Results.BadRequest(new { error = "Grade must be between 1 and 12." });
        }

        lesson.Grade = request.Grade.Value;
    }

    await db.SaveChangesAsync();

    return Results.Ok(new LessonSummaryResponse(
        lesson.Id,
        lesson.Title,
        lesson.Subject,
        lesson.Grade,
        lesson.Topic,
        lesson.Difficulty,
        lesson.CreatedAt,
        await db.Questions.CountAsync(x => x.LessonId == lesson.Id)));
});

parentApi.MapDelete("/lessons/{lessonId:guid}", async (AppDbContext db, ClaimsPrincipal user, Guid lessonId) =>
{
    var parentId = ResolveUserId(user);
    if (!parentId.HasValue)
    {
        return Results.Unauthorized();
    }

    var lesson = await db.Lessons.FirstOrDefaultAsync(x => x.Id == lessonId && x.CreatedBy == parentId.Value);
    if (lesson is null)
    {
        return Results.NotFound();
    }

    var hasAssignments = await db.Assignments.AnyAsync(x => x.LessonId == lesson.Id);
    if (hasAssignments)
    {
        return Results.Conflict(new { error = "Cannot delete a lesson with assignments." });
    }

    db.Lessons.Remove(lesson);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

parentApi.MapPost("/assignments", async (AppDbContext db, ClaimsPrincipal user, CreateAssignmentRequest request) =>
{
    var parentId = ResolveUserId(user);
    if (!parentId.HasValue)
    {
        return Results.Unauthorized();
    }

    var child = await db.Children.FirstOrDefaultAsync(x => x.Id == request.ChildId && x.ParentId == parentId.Value);
    if (child is null)
    {
        return Results.BadRequest(new { error = "Child does not belong to current parent." });
    }

    var lesson = await db.Lessons.FirstOrDefaultAsync(x => x.Id == request.LessonId && x.CreatedBy == parentId.Value);
    if (lesson is null)
    {
        return Results.BadRequest(new { error = "Lesson does not belong to current parent." });
    }

    var assignment = new Assignment
    {
        ChildId = request.ChildId,
        LessonId = request.LessonId,
        AssignedAt = DateTime.UtcNow,
        DueDate = request.DueDate,
        Status = "Assigned"
    };

    db.Assignments.Add(assignment);
    await db.SaveChangesAsync();

    return Results.Created($"/api/v1/assignments/{assignment.Id}", new AssignmentResponse(
        assignment.Id,
        assignment.ChildId,
        assignment.LessonId,
        assignment.AssignedAt,
        assignment.DueDate,
        assignment.Status));
});

parentApi.MapGet("/assignments", async (AppDbContext db, ClaimsPrincipal user, Guid? childId) =>
{
    var parentId = ResolveUserId(user);
    if (!parentId.HasValue)
    {
        return Results.Unauthorized();
    }

    var query = db.Assignments
        .AsNoTracking()
        .Where(x => x.Child.ParentId == parentId.Value);

    if (childId.HasValue)
    {
        query = query.Where(x => x.ChildId == childId.Value);
    }

    var assignments = await query
        .OrderByDescending(x => x.AssignedAt)
        .Select(x => new AssignmentResponse(
            x.Id,
            x.ChildId,
            x.LessonId,
            x.AssignedAt,
            x.DueDate,
            x.Status))
        .ToListAsync();

    return Results.Ok(assignments);
});

parentApi.MapGet("/assignments/{assignmentId:guid}/for-solving", async (AppDbContext db, ClaimsPrincipal user, Guid assignmentId) =>
{
    var parentId = ResolveUserId(user);
    if (!parentId.HasValue)
    {
        return Results.Unauthorized();
    }

    var assignment = await db.Assignments
        .AsNoTracking()
        .Include(x => x.Lesson)
        .ThenInclude(x => x.Questions.OrderBy(q => q.Order))
        .ThenInclude(q => q.Answers.OrderBy(a => a.Order))
        .FirstOrDefaultAsync(x => x.Id == assignmentId && x.Child.ParentId == parentId.Value);

    if (assignment is null)
    {
        return Results.NotFound();
    }

    var response = new AssignmentForSolvingResponse(
        assignment.Id,
        assignment.ChildId,
        assignment.LessonId,
        assignment.AssignedAt,
        assignment.DueDate,
        assignment.Status,
        assignment.Lesson.Title,
        assignment.Lesson.Questions
            .OrderBy(q => q.Order)
            .Select(q => new AssignmentQuestionResponse(
                q.Id,
                q.QuestionText,
                q.Explanation,
                q.Order,
                q.Answers
                    .OrderBy(a => a.Order)
                    .Select(a => new AssignmentQuestionAnswerResponse(a.Id, a.AnswerText, a.Order))
                    .ToList()))
            .ToList());

    return Results.Ok(response);
});

parentApi.MapPost("/assignments/{assignmentId:guid}/answers", async (AppDbContext db, ClaimsPrincipal user, Guid assignmentId, SubmitAssignmentAnswersRequest request) =>
{
    var parentId = ResolveUserId(user);
    if (!parentId.HasValue)
    {
        return Results.Unauthorized();
    }

    if (request.Answers is null || request.Answers.Count == 0)
    {
        return Results.BadRequest(new { error = "At least one answer is required." });
    }

    var assignment = await db.Assignments
        .Include(x => x.Lesson)
        .ThenInclude(x => x.Questions)
        .ThenInclude(q => q.Answers)
        .FirstOrDefaultAsync(x => x.Id == assignmentId && x.Child.ParentId == parentId.Value);

    if (assignment is null)
    {
        return Results.NotFound();
    }

    var questionsById = assignment.Lesson.Questions.ToDictionary(q => q.Id);
    var instantCheck = new List<InstantCheckItemResponse>();

    foreach (var answer in request.Answers)
    {
        if (!questionsById.TryGetValue(answer.QuestionId, out var question))
        {
            return Results.BadRequest(new { error = "Question does not belong to assignment lesson." });
        }

        var normalizedTextAnswer = string.IsNullOrWhiteSpace(answer.TextAnswer)
            ? null
            : answer.TextAnswer.Trim();

        var isCorrect = false;
        if (answer.SelectedAnswerOptionId.HasValue)
        {
            var selected = question.Answers.FirstOrDefault(x => x.Id == answer.SelectedAnswerOptionId.Value);
            if (selected is null)
            {
                return Results.BadRequest(new { error = "Selected answer option does not belong to question." });
            }

            isCorrect = selected.IsCorrect;
        }
        else if (!string.IsNullOrWhiteSpace(normalizedTextAnswer))
        {
            isCorrect = question.Answers.Any(x => x.IsCorrect && string.Equals(x.AnswerText.Trim(), normalizedTextAnswer, StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            return Results.BadRequest(new { error = "Either SelectedAnswerOptionId or TextAnswer is required for each answer." });
        }

        var existing = await db.AssignmentAnswers
            .FirstOrDefaultAsync(x => x.AssignmentId == assignment.Id && x.QuestionId == answer.QuestionId);

        if (existing is null)
        {
            db.AssignmentAnswers.Add(new AssignmentAnswer
            {
                AssignmentId = assignment.Id,
                QuestionId = answer.QuestionId,
                SelectedAnswerOptionId = answer.SelectedAnswerOptionId,
                TextAnswer = normalizedTextAnswer,
                IsCorrect = isCorrect,
                SubmittedAt = DateTime.UtcNow
            });
        }
        else
        {
            existing.SelectedAnswerOptionId = answer.SelectedAnswerOptionId;
            existing.TextAnswer = normalizedTextAnswer;
            existing.IsCorrect = isCorrect;
            existing.SubmittedAt = DateTime.UtcNow;
        }

        instantCheck.Add(new InstantCheckItemResponse(question.Id, isCorrect, question.Explanation));
    }

    if (assignment.Status == "Assigned")
    {
        assignment.Status = "InProgress";
    }

    await db.SaveChangesAsync();

    var totalQuestions = assignment.Lesson.Questions.Count;
    var correctCount = await db.AssignmentAnswers.CountAsync(x => x.AssignmentId == assignment.Id && x.IsCorrect);
    var partialScore = totalQuestions == 0 ? 0 : Math.Round(100m * correctCount / totalQuestions, 2);

    return Results.Ok(new SubmitAssignmentAnswersResponse(instantCheck, partialScore));
});

parentApi.MapPost("/assignments/{assignmentId:guid}/complete", async (AppDbContext db, ClaimsPrincipal user, Guid assignmentId) =>
{
    var parentId = ResolveUserId(user);
    if (!parentId.HasValue)
    {
        return Results.Unauthorized();
    }

    var assignment = await db.Assignments
        .Include(x => x.Lesson)
        .ThenInclude(x => x.Questions)
        .FirstOrDefaultAsync(x => x.Id == assignmentId && x.Child.ParentId == parentId.Value);

    if (assignment is null)
    {
        return Results.NotFound();
    }

    var totalQuestions = assignment.Lesson.Questions.Count;
    var correctCount = await db.AssignmentAnswers.CountAsync(x => x.AssignmentId == assignment.Id && x.IsCorrect);
    var score = totalQuestions == 0 ? 0 : Math.Round(100m * correctCount / totalQuestions, 2);
    var completedAt = DateTime.UtcNow;

    var result = await db.Results.FirstOrDefaultAsync(x => x.AssignmentId == assignment.Id);

    if (result is null)
    {
        result = new AssignmentResult
        {
            AssignmentId = assignment.Id,
            Score = score,
            CorrectAnswers = correctCount,
            TotalQuestions = totalQuestions,
            CompletedAt = completedAt
        };

        db.Results.Add(result);
    }
    else
    {
        result.Score = score;
        result.CorrectAnswers = correctCount;
        result.TotalQuestions = totalQuestions;
        result.CompletedAt = completedAt;
    }

    assignment.Status = "Completed";
    await db.SaveChangesAsync();

    return Results.Ok(new CompleteAssignmentResponse(
        result.Id,
        result.Score,
        result.CompletedAt,
        result.CorrectAnswers,
        result.TotalQuestions));
});

parentApi.MapGet("/results/{resultId:guid}", async (AppDbContext db, ClaimsPrincipal user, Guid resultId) =>
{
    var parentId = ResolveUserId(user);
    if (!parentId.HasValue)
    {
        return Results.Unauthorized();
    }

    var result = await db.Results
        .AsNoTracking()
        .Include(x => x.Assignment)
        .FirstOrDefaultAsync(x => x.Id == resultId && x.Assignment.Child.ParentId == parentId.Value);

    if (result is null)
    {
        return Results.NotFound();
    }

    var breakdown = await db.AssignmentAnswers
        .AsNoTracking()
        .Where(x => x.AssignmentId == result.AssignmentId)
        .OrderBy(x => x.SubmittedAt)
        .Select(x => new ResultBreakdownItemResponse(x.QuestionId, x.IsCorrect))
        .ToListAsync();

    return Results.Ok(new ResultDetailResponse(
        result.Id,
        result.AssignmentId,
        result.Score,
        result.CompletedAt,
        result.CorrectAnswers,
        result.TotalQuestions,
        breakdown));
});

var childApi = apiV1.MapGroup("/child")
    .RequireAuthorization("ChildOnly");

childApi.MapGet("/assignments", async (AppDbContext db, ClaimsPrincipal user) =>
{
    var childId = await ResolveChildIdAsync(db, user);
    if (!childId.HasValue)
    {
        return Results.Unauthorized();
    }

    var assignments = await db.Assignments
        .AsNoTracking()
        .Where(x => x.ChildId == childId.Value)
        .OrderByDescending(x => x.AssignedAt)
        .Select(x => new AssignmentResponse(
            x.Id,
            x.ChildId,
            x.LessonId,
            x.AssignedAt,
            x.DueDate,
            x.Status))
        .ToListAsync();

    return Results.Ok(assignments);
});

childApi.MapGet("/assignments/{assignmentId:guid}/for-solving", async (AppDbContext db, ClaimsPrincipal user, Guid assignmentId) =>
{
    var childId = await ResolveChildIdAsync(db, user);
    if (!childId.HasValue)
    {
        return Results.Unauthorized();
    }

    var assignment = await db.Assignments
        .AsNoTracking()
        .Include(x => x.Lesson)
        .ThenInclude(x => x.Questions.OrderBy(q => q.Order))
        .ThenInclude(q => q.Answers.OrderBy(a => a.Order))
        .FirstOrDefaultAsync(x => x.Id == assignmentId && x.ChildId == childId.Value);

    if (assignment is null)
    {
        return Results.NotFound();
    }

    var response = new AssignmentForSolvingResponse(
        assignment.Id,
        assignment.ChildId,
        assignment.LessonId,
        assignment.AssignedAt,
        assignment.DueDate,
        assignment.Status,
        assignment.Lesson.Title,
        assignment.Lesson.Questions
            .OrderBy(q => q.Order)
            .Select(q => new AssignmentQuestionResponse(
                q.Id,
                q.QuestionText,
                q.Explanation,
                q.Order,
                q.Answers
                    .OrderBy(a => a.Order)
                    .Select(a => new AssignmentQuestionAnswerResponse(a.Id, a.AnswerText, a.Order))
                    .ToList()))
            .ToList());

    return Results.Ok(response);
});

childApi.MapPost("/assignments/{assignmentId:guid}/answers", async (AppDbContext db, ClaimsPrincipal user, Guid assignmentId, SubmitAssignmentAnswersRequest request) =>
{
    var childId = await ResolveChildIdAsync(db, user);
    if (!childId.HasValue)
    {
        return Results.Unauthorized();
    }

    if (request.Answers is null || request.Answers.Count == 0)
    {
        return Results.BadRequest(new { error = "At least one answer is required." });
    }

    var assignment = await db.Assignments
        .Include(x => x.Lesson)
        .ThenInclude(x => x.Questions)
        .ThenInclude(q => q.Answers)
        .FirstOrDefaultAsync(x => x.Id == assignmentId && x.ChildId == childId.Value);

    if (assignment is null)
    {
        return Results.NotFound();
    }

    var questionsById = assignment.Lesson.Questions.ToDictionary(q => q.Id);
    var instantCheck = new List<InstantCheckItemResponse>();

    foreach (var answer in request.Answers)
    {
        if (!questionsById.TryGetValue(answer.QuestionId, out var question))
        {
            return Results.BadRequest(new { error = "Question does not belong to assignment lesson." });
        }

        var normalizedTextAnswer = string.IsNullOrWhiteSpace(answer.TextAnswer)
            ? null
            : answer.TextAnswer.Trim();

        var isCorrect = false;
        if (answer.SelectedAnswerOptionId.HasValue)
        {
            var selected = question.Answers.FirstOrDefault(x => x.Id == answer.SelectedAnswerOptionId.Value);
            if (selected is null)
            {
                return Results.BadRequest(new { error = "Selected answer option does not belong to question." });
            }

            isCorrect = selected.IsCorrect;
        }
        else if (!string.IsNullOrWhiteSpace(normalizedTextAnswer))
        {
            isCorrect = question.Answers.Any(x => x.IsCorrect && string.Equals(x.AnswerText.Trim(), normalizedTextAnswer, StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            return Results.BadRequest(new { error = "Either SelectedAnswerOptionId or TextAnswer is required for each answer." });
        }

        var existing = await db.AssignmentAnswers
            .FirstOrDefaultAsync(x => x.AssignmentId == assignment.Id && x.QuestionId == answer.QuestionId);

        if (existing is null)
        {
            db.AssignmentAnswers.Add(new AssignmentAnswer
            {
                AssignmentId = assignment.Id,
                QuestionId = answer.QuestionId,
                SelectedAnswerOptionId = answer.SelectedAnswerOptionId,
                TextAnswer = normalizedTextAnswer,
                IsCorrect = isCorrect,
                SubmittedAt = DateTime.UtcNow
            });
        }
        else
        {
            existing.SelectedAnswerOptionId = answer.SelectedAnswerOptionId;
            existing.TextAnswer = normalizedTextAnswer;
            existing.IsCorrect = isCorrect;
            existing.SubmittedAt = DateTime.UtcNow;
        }

        instantCheck.Add(new InstantCheckItemResponse(question.Id, isCorrect, question.Explanation));
    }

    if (assignment.Status == "Assigned")
    {
        assignment.Status = "InProgress";
    }

    await db.SaveChangesAsync();

    var totalQuestions = assignment.Lesson.Questions.Count;
    var correctCount = await db.AssignmentAnswers.CountAsync(x => x.AssignmentId == assignment.Id && x.IsCorrect);
    var partialScore = totalQuestions == 0 ? 0 : Math.Round(100m * correctCount / totalQuestions, 2);

    return Results.Ok(new SubmitAssignmentAnswersResponse(instantCheck, partialScore));
});

childApi.MapPost("/assignments/{assignmentId:guid}/complete", async (AppDbContext db, ClaimsPrincipal user, Guid assignmentId) =>
{
    var childId = await ResolveChildIdAsync(db, user);
    if (!childId.HasValue)
    {
        return Results.Unauthorized();
    }

    var assignment = await db.Assignments
        .Include(x => x.Lesson)
        .ThenInclude(x => x.Questions)
        .FirstOrDefaultAsync(x => x.Id == assignmentId && x.ChildId == childId.Value);

    if (assignment is null)
    {
        return Results.NotFound();
    }

    var totalQuestions = assignment.Lesson.Questions.Count;
    var correctCount = await db.AssignmentAnswers.CountAsync(x => x.AssignmentId == assignment.Id && x.IsCorrect);
    var score = totalQuestions == 0 ? 0 : Math.Round(100m * correctCount / totalQuestions, 2);
    var completedAt = DateTime.UtcNow;

    var result = await db.Results.FirstOrDefaultAsync(x => x.AssignmentId == assignment.Id);
    if (result is null)
    {
        result = new AssignmentResult
        {
            AssignmentId = assignment.Id,
            Score = score,
            CorrectAnswers = correctCount,
            TotalQuestions = totalQuestions,
            CompletedAt = completedAt
        };
        db.Results.Add(result);
    }
    else
    {
        result.Score = score;
        result.CorrectAnswers = correctCount;
        result.TotalQuestions = totalQuestions;
        result.CompletedAt = completedAt;
    }

    assignment.Status = "Completed";
    await db.SaveChangesAsync();

    return Results.Ok(new CompleteAssignmentResponse(
        result.Id,
        result.Score,
        result.CompletedAt,
        result.CorrectAnswers,
        result.TotalQuestions));
});

childApi.MapGet("/results/{resultId:guid}", async (AppDbContext db, ClaimsPrincipal user, Guid resultId) =>
{
    var childId = await ResolveChildIdAsync(db, user);
    if (!childId.HasValue)
    {
        return Results.Unauthorized();
    }

    var result = await db.Results
        .AsNoTracking()
        .Include(x => x.Assignment)
        .FirstOrDefaultAsync(x => x.Id == resultId && x.Assignment.ChildId == childId.Value);

    if (result is null)
    {
        return Results.NotFound();
    }

    var breakdown = await db.AssignmentAnswers
        .AsNoTracking()
        .Where(x => x.AssignmentId == result.AssignmentId)
        .OrderBy(x => x.SubmittedAt)
        .Select(x => new ResultBreakdownItemResponse(x.QuestionId, x.IsCorrect))
        .ToListAsync();

    return Results.Ok(new ResultDetailResponse(
        result.Id,
        result.AssignmentId,
        result.Score,
        result.CompletedAt,
        result.CorrectAnswers,
        result.TotalQuestions,
        breakdown));
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
