using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MediatR;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<Program>());
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(RequestLoggingBehavior<,>));
builder.Services.AddTransient<IRequestValidator<CreateParentLessonCommand>, CreateParentLessonCommandValidator>();
builder.Services.AddTransient<IRequestValidator<DuplicateParentLessonCommand>, DuplicateParentLessonCommandValidator>();
builder.Services.AddTransient<IRequestValidator<UpdateParentLessonCommand>, UpdateParentLessonCommandValidator>();
builder.Services.AddTransient<IRequestValidator<DeleteParentLessonCommand>, DeleteParentLessonCommandValidator>();
builder.Services.AddTransient<IRequestValidator<CreateParentAssignmentCommand>, CreateParentAssignmentCommandValidator>();
builder.Services.AddTransient<IRequestValidator<SubmitParentAssignmentAnswersCommand>, SubmitParentAssignmentAnswersCommandValidator>();
builder.Services.AddTransient<IRequestValidator<CompleteParentAssignmentCommand>, CompleteParentAssignmentCommandValidator>();
builder.Services.AddTransient<IRequestValidator<GetParentAssignmentForSolvingQuery>, GetParentAssignmentForSolvingQueryValidator>();
builder.Services.AddTransient<IRequestValidator<GetParentResultDetailQuery>, GetParentResultDetailQueryValidator>();
builder.Services.AddTransient<IRequestValidator<GetChildAssignmentForSolvingQuery>, GetChildAssignmentForSolvingQueryValidator>();
builder.Services.AddTransient<IRequestValidator<SubmitChildAssignmentAnswersCommand>, SubmitChildAssignmentAnswersCommandValidator>();
builder.Services.AddTransient<IRequestValidator<CompleteChildAssignmentCommand>, CompleteChildAssignmentCommandValidator>();
builder.Services.AddTransient<IRequestValidator<GetChildResultDetailQuery>, GetChildResultDetailQueryValidator>();

JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

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

builder.Services.AddDbContext<AppDbContext>(opt => opt.UseNpgsql(connectionString));

builder.Services.AddScoped<IPasswordHasherService, PasswordHasherService>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IAssignmentSolvingService, AssignmentSolvingService>();
builder.Services.AddScoped<IAssignmentReadService, AssignmentReadService>();
builder.Services.AddScoped<IAiLessonGenerationService, AiLessonGenerationService>();
builder.Services.AddScoped<IAiLessonEditingService, AiLessonEditingService>();
builder.Services.AddHttpClient<IAIProvider, OpenAiProvider>();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 120,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

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

app.UseExceptionHandler(exceptionHandlerApp =>
{
    exceptionHandlerApp.Run(async context =>
    {
        var exceptionFeature = context.Features.Get<IExceptionHandlerFeature>();
        var detail = app.Environment.IsDevelopment()
            ? exceptionFeature?.Error.Message
            : null;

        var problemResult = Results.Problem(
            title: "Unexpected server error",
            detail: detail,
            statusCode: StatusCodes.Status500InternalServerError);

        await problemResult.ExecuteAsync(context);
    });
});

app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("HttpRequest");
    var startedAt = DateTime.UtcNow;

    try
    {
        await next();
        var elapsedMs = (DateTime.UtcNow - startedAt).TotalMilliseconds;
        logger.LogInformation(
            "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs:0.00} ms",
            context.Request.Method,
            context.Request.Path,
            context.Response.StatusCode,
            elapsedMs);
    }
    catch (Exception ex)
    {
        var elapsedMs = (DateTime.UtcNow - startedAt).TotalMilliseconds;
        logger.LogError(
            ex,
            "HTTP {Method} {Path} failed in {ElapsedMs:0.00} ms",
            context.Request.Method,
            context.Request.Path,
            elapsedMs);
        throw;
    }
});

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    if (!db.Database.IsRelational())
    {
        db.Database.EnsureCreated();
    }
    else if (db.Database.GetMigrations().Any())
    {
        db.Database.Migrate();
    }
    else
    {
        db.Database.EnsureCreated();
    }
}

app.UseCors("AllowFrontend");
app.UseRateLimiter();

app.Use(async (context, next) =>
{
    context.Response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
    context.Response.Headers.TryAdd("X-Frame-Options", "DENY");
    context.Response.Headers.TryAdd("Referrer-Policy", "no-referrer");
    context.Response.Headers.TryAdd("X-Permitted-Cross-Domain-Policies", "none");
    await next();
});

app.UseDefaultFiles();
app.UseStaticFiles();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.MapGet("/health/live", () => Results.Ok(new { status = "alive" }));
app.MapGet("/health/ready", async (AppDbContext db) =>
{
    if (!db.Database.IsRelational())
    {
        return Results.Ok(new { status = "ready", database = "in-memory" });
    }

    var canConnect = await db.Database.CanConnectAsync();
    if (!canConnect)
    {
        return Results.Json(new { status = "degraded", database = "unreachable" }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    return Results.Ok(new { status = "ready", database = "reachable" });
});

var apiV1 = app.MapGroup("/api/v1");
apiV1.MapGet("/health", () => Results.Ok(new { status = "healthy", version = "v1" }));
apiV1.MapGet("/health/live", () => Results.Ok(new { status = "alive", version = "v1" }));
apiV1.MapGet("/health/ready", async (AppDbContext db) =>
{
    if (!db.Database.IsRelational())
    {
        return Results.Ok(new { status = "ready", database = "in-memory", version = "v1" });
    }

    var canConnect = await db.Database.CanConnectAsync();
    if (!canConnect)
    {
        return Results.Json(new { status = "degraded", database = "unreachable", version = "v1" }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    return Results.Ok(new { status = "ready", database = "reachable", version = "v1" });
});

apiV1.MapAuthController();
apiV1.MapParentController();
apiV1.MapChildController();

app.MapFallbackToFile("index.html");

app.Run();

public partial class Program;
