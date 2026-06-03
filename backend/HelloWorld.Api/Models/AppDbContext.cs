using Microsoft.EntityFrameworkCore;

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
