using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

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
var connectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? "Host=localhost;Database=helloworld;Username=postgres;Password=postgres";

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(connectionString));

var app = builder.Build();

// Auto-migrate on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.UseCors("AllowFrontend");
app.UseDefaultFiles();
app.UseStaticFiles();

// Health check
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

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

// ── Models ────────────────────────────────────────────────────────────────────

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Greeting> Greetings => Set<Greeting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Greeting>().HasData(
            new Greeting { Id = 1, Text = "Hello, World!", CreatedAt = DateTime.UtcNow }
        );
    }
}

public class Greeting
{
    public int Id { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public record GreetingRequest(string Message);
