using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KidsLearn.Api.EFMigration
{
    /// <inheritdoc />
    public partial class AddChildAuthLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "Children",
                type: "uuid",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Greetings",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 6, 4, 15, 44, 38, 973, DateTimeKind.Utc).AddTicks(4857));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2026, 6, 4, 15, 44, 38, 973, DateTimeKind.Utc).AddTicks(4999));

            migrationBuilder.CreateIndex(
                name: "IX_Children_UserId",
                table: "Children",
                column: "UserId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Children_Users_UserId",
                table: "Children",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Children_Users_UserId",
                table: "Children");

            migrationBuilder.DropIndex(
                name: "IX_Children_UserId",
                table: "Children");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Children");

            migrationBuilder.UpdateData(
                table: "Greetings",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 6, 4, 15, 36, 3, 93, DateTimeKind.Utc).AddTicks(5452));

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2026, 6, 4, 15, 36, 3, 93, DateTimeKind.Utc).AddTicks(5596));
        }
    }
}
