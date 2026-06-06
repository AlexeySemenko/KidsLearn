using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KidsLearn.Api.EFMigration
{
    /// <inheritdoc />
    public partial class AddAssignmentSolvingFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CorrectAnswers",
                table: "Results",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TotalQuestions",
                table: "Results",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "AssignmentAnswers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SelectedAnswerOptionId = table.Column<Guid>(type: "uuid", nullable: true),
                    TextAnswer = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsCorrect = table.Column<bool>(type: "boolean", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssignmentAnswers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssignmentAnswers_AnswerOptions_SelectedAnswerOptionId",
                        column: x => x.SelectedAnswerOptionId,
                        principalTable: "AnswerOptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AssignmentAnswers_Assignments_AssignmentId",
                        column: x => x.AssignmentId,
                        principalTable: "Assignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AssignmentAnswers_Questions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "Questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2026, 6, 4, 15, 36, 3, 93, DateTimeKind.Utc).AddTicks(5596));

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentAnswers_AssignmentId_QuestionId",
                table: "AssignmentAnswers",
                columns: new[] { "AssignmentId", "QuestionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentAnswers_QuestionId",
                table: "AssignmentAnswers",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentAnswers_SelectedAnswerOptionId",
                table: "AssignmentAnswers",
                column: "SelectedAnswerOptionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AssignmentAnswers");

            migrationBuilder.DropColumn(
                name: "CorrectAnswers",
                table: "Results");

            migrationBuilder.DropColumn(
                name: "TotalQuestions",
                table: "Results");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2026, 6, 4, 15, 20, 11, 749, DateTimeKind.Utc).AddTicks(5344));
        }
    }
}
