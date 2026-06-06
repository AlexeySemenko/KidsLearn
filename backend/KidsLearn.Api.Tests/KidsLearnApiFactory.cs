using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

public sealed class KidsLearnApiFactory : WebApplicationFactory<Program>
{
    private const string ParentEmail = "parent.test@example.com";
    private const string ParentPassword = "Parent123!";
    private readonly string _databaseName = $"kidslearn-tests-{Guid.NewGuid():N}";
    private readonly Action<IServiceCollection>? _configureServices;

    public KidsLearnApiFactory(Action<IServiceCollection>? configureServices = null)
    {
        _configureServices = configureServices;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            var dbDescriptors = services
                .Where(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>) || d.ServiceType == typeof(AppDbContext))
                .ToList();

            foreach (var descriptor in dbDescriptors)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseInMemoryDatabase(_databaseName);
            });

            _configureServices?.Invoke(services);

            using var scope = services.BuildServiceProvider().CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureCreated();
            Seed(db);
        });
    }

    private static void Seed(AppDbContext db)
    {
        if (db.Users.Any(x => x.Email == ParentEmail))
        {
            return;
        }

        var hasher = new PasswordHasherService();
        db.Users.Add(new AppUser
        {
            Email = ParentEmail,
            PasswordHash = hasher.HashPassword(ParentPassword),
            Role = UserRole.Parent,
            CreatedAt = DateTime.UtcNow
        });

        db.SaveChanges();
    }
}
