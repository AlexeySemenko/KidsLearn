using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KidsLearn.Api.EFMigration
{
    /// <inheritdoc />
    public partial class AddChildFriendships : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChildFriendships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequesterId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcceptorId = table.Column<Guid>(type: "uuid", nullable: true),
                    InviteeEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    InviteToken = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AcceptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChildFriendships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChildFriendships_Children_AcceptorId",
                        column: x => x.AcceptorId,
                        principalTable: "Children",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ChildFriendships_Children_RequesterId",
                        column: x => x.RequesterId,
                        principalTable: "Children",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChildFriendships_AcceptorId",
                table: "ChildFriendships",
                column: "AcceptorId");

            migrationBuilder.CreateIndex(
                name: "IX_ChildFriendships_InviteToken",
                table: "ChildFriendships",
                column: "InviteToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChildFriendships_RequesterId",
                table: "ChildFriendships",
                column: "RequesterId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ChildFriendships");
        }
    }
}
