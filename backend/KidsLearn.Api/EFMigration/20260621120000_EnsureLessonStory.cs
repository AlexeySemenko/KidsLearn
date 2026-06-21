using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KidsLearn.Api.EFMigration
{
    /// <inheritdoc />
    public partial class EnsureLessonStory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotent: adds the column only if it does not already exist.
            // Required because 20260621000000_AddLessonStory was recorded in
            // __EFMigrationsHistory on a previous run but the ALTER TABLE was
            // never executed against the live database.
            migrationBuilder.Sql("""
                ALTER TABLE "Lessons" ADD COLUMN IF NOT EXISTS "Story" text;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Story",
                table: "Lessons");
        }
    }
}
