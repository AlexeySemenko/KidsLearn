using Microsoft.EntityFrameworkCore;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Child> Children => Set<Child>();
    public DbSet<Lesson> Lessons => Set<Lesson>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<AnswerOption> AnswerOptions => Set<AnswerOption>();
    public DbSet<LessonRevision> LessonRevisions => Set<LessonRevision>();
    public DbSet<Assignment> Assignments => Set<Assignment>();
    public DbSet<AssignmentAnswer> AssignmentAnswers => Set<AssignmentAnswer>();
    public DbSet<AssignmentResult> Results => Set<AssignmentResult>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<ParentAccountLink> ParentAccountLinks => Set<ParentAccountLink>();
    public DbSet<ChildFriendship> ChildFriendships => Set<ChildFriendship>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Email).IsUnique();
            entity.HasIndex(x => new { x.ExternalProvider, x.ExternalSubject }).IsUnique();
            entity.Property(x => x.Email).HasMaxLength(320);
            entity.Property(x => x.PasswordHash).HasMaxLength(500);
            entity.Property(x => x.ExternalProvider).HasMaxLength(50);
            entity.Property(x => x.ExternalSubject).HasMaxLength(255);
            entity.Property(x => x.DisplayName).HasMaxLength(200);
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Token).IsUnique();
            entity.Property(x => x.Token).HasMaxLength(256);
            entity.HasOne(x => x.User)
                .WithMany(x => x.RefreshTokens)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ParentAccountLink>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.ParentAId, x.ParentBId }).IsUnique();

            entity.HasOne(x => x.ParentA)
                .WithMany()
                .HasForeignKey(x => x.ParentAId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.ParentB)
                .WithMany()
                .HasForeignKey(x => x.ParentBId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Child>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(100);
            entity.HasIndex(x => x.UserId).IsUnique();
            entity.HasOne(x => x.Parent)
                .WithMany(x => x.Children)
                .HasForeignKey(x => x.ParentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.User)
                .WithOne(x => x.ChildProfile)
                .HasForeignKey<Child>(x => x.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Lesson>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).HasMaxLength(200);
            entity.Property(x => x.Subject).HasMaxLength(100);
            entity.Property(x => x.Topic).HasMaxLength(200);
            entity.Property(x => x.Difficulty).HasMaxLength(50);
        });

        modelBuilder.Entity<Question>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasOne(x => x.Lesson)
                .WithMany(x => x.Questions)
                .HasForeignKey(x => x.LessonId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AnswerOption>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasOne(x => x.Question)
                .WithMany(x => x.Answers)
                .HasForeignKey(x => x.QuestionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LessonRevision>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.SnapshotJson).HasColumnType("text");
            entity.Property(x => x.DiffSummary).HasMaxLength(500);
            entity.HasIndex(x => new { x.LessonId, x.RevisionNumber }).IsUnique();

            entity.HasOne(x => x.Lesson)
                .WithMany(x => x.Revisions)
                .HasForeignKey(x => x.LessonId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Assignment>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).HasMaxLength(30);
            entity.HasOne(x => x.Child)
                .WithMany(x => x.Assignments)
                .HasForeignKey(x => x.ChildId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Lesson)
                .WithMany(x => x.Assignments)
                .HasForeignKey(x => x.LessonId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AssignmentAnswer>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TextAnswer).HasMaxLength(500);
            entity.HasIndex(x => new { x.AssignmentId, x.QuestionId }).IsUnique();

            entity.HasOne(x => x.Assignment)
                .WithMany(x => x.Answers)
                .HasForeignKey(x => x.AssignmentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Question)
                .WithMany(x => x.AssignmentAnswers)
                .HasForeignKey(x => x.QuestionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.SelectedAnswerOption)
                .WithMany()
                .HasForeignKey(x => x.SelectedAnswerOptionId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<AssignmentResult>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasOne(x => x.Assignment)
                .WithOne(x => x.Result)
                .HasForeignKey<AssignmentResult>(x => x.AssignmentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ChildFriendship>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.InviteToken).IsUnique();
            entity.Property(x => x.InviteeEmail).HasMaxLength(320);
            entity.Property(x => x.InviteToken).HasMaxLength(128);
            entity.Property(x => x.Status).HasMaxLength(20);
            entity.Property(x => x.NoteFromRequester).HasMaxLength(500);
            entity.Property(x => x.NoteFromAcceptor).HasMaxLength(500);

            entity.HasOne(x => x.Requester)
                .WithMany()
                .HasForeignKey(x => x.RequesterId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Acceptor)
                .WithMany()
                .HasForeignKey(x => x.AcceptorId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        var defaultParentId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        modelBuilder.Entity<AppUser>().HasData(
            new AppUser
            {
                Id = defaultParentId,
                Email = "parent@example.com",
                PasswordHash = "",
                Role = UserRole.Parent,
                CreatedAt = DateTime.UtcNow
            }
        );
    }
}
