using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KidsLearn.Api.EFMigration
{
    /// <inheritdoc />
    public partial class AddFriendNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NoteFromRequester",
                table: "ChildFriendships",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NoteFromRequesterAt",
                table: "ChildFriendships",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NoteFromRequesterReadAt",
                table: "ChildFriendships",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NoteFromAcceptor",
                table: "ChildFriendships",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NoteFromAcceptorAt",
                table: "ChildFriendships",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NoteFromAcceptorReadAt",
                table: "ChildFriendships",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "NoteFromRequester",     table: "ChildFriendships");
            migrationBuilder.DropColumn(name: "NoteFromRequesterAt",   table: "ChildFriendships");
            migrationBuilder.DropColumn(name: "NoteFromRequesterReadAt", table: "ChildFriendships");
            migrationBuilder.DropColumn(name: "NoteFromAcceptor",      table: "ChildFriendships");
            migrationBuilder.DropColumn(name: "NoteFromAcceptorAt",    table: "ChildFriendships");
            migrationBuilder.DropColumn(name: "NoteFromAcceptorReadAt", table: "ChildFriendships");
        }
    }
}
