using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KidsLearn.Api.EFMigration
{
    /// <inheritdoc />
    public partial class AddParentAccountLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ParentAccountLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentAId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentBId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParentAccountLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParentAccountLinks_Users_ParentAId",
                        column: x => x.ParentAId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ParentAccountLinks_Users_ParentBId",
                        column: x => x.ParentBId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2026, 6, 11, 10, 16, 19, 195, DateTimeKind.Utc).AddTicks(9410));

            migrationBuilder.CreateIndex(
                name: "IX_ParentAccountLinks_ParentAId_ParentBId",
                table: "ParentAccountLinks",
                columns: new[] { "ParentAId", "ParentBId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParentAccountLinks_ParentBId",
                table: "ParentAccountLinks",
                column: "ParentBId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ParentAccountLinks");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2026, 6, 10, 18, 26, 50, 710, DateTimeKind.Utc).AddTicks(7646));
        }
    }
}
