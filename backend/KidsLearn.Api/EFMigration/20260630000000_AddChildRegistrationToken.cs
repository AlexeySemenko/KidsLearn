using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KidsLearn.Api.EFMigration
{
    /// <inheritdoc />
    public partial class AddChildRegistrationToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RegistrationToken",
                table: "Children",
                type: "character varying(36)",
                maxLength: 36,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Children_RegistrationToken",
                table: "Children",
                column: "RegistrationToken",
                unique: true,
                filter: "\"RegistrationToken\" IS NOT NULL");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2026, 6, 30, 0, 0, 0, 0, DateTimeKind.Utc));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Children_RegistrationToken",
                table: "Children");

            migrationBuilder.DropColumn(
                name: "RegistrationToken",
                table: "Children");

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "CreatedAt",
                value: new DateTime(2026, 6, 29, 12, 15, 56, 862, DateTimeKind.Utc).AddTicks(2929));
        }
    }
}
