using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KidsLearn.Api.EFMigration
{
    /// <inheritdoc />
    public partial class AddGoogleParentSso : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EmailVerified",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ExternalProvider",
                table: "Users",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalSubject",
                table: "Users",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                columns: new[] { "CreatedAt", "EmailVerified", "ExternalProvider", "ExternalSubject" },
                values: new object[] { new DateTime(2026, 6, 10, 18, 26, 50, 710, DateTimeKind.Utc).AddTicks(7646), false, null, null });

            migrationBuilder.CreateIndex(
                name: "IX_Users_ExternalProvider_ExternalSubject",
                table: "Users",
                columns: new[] { "ExternalProvider", "ExternalSubject" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_ExternalProvider_ExternalSubject",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "EmailVerified",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ExternalProvider",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ExternalSubject",
                table: "Users");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2026, 6, 4, 16, 21, 40, 753, DateTimeKind.Utc).AddTicks(9717));
        }
    }
}
