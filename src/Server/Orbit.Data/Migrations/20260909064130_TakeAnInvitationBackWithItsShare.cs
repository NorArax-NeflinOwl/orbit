using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Data.Migrations
{
    /// <inheritdoc />
    public partial class TakeAnInvitationBackWithItsShare : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OP_C_ANNOUNCESSHAREID",
                table: "OP_CHATS",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OP_CHATS_OP_C_ANNOUNCESSHAREID",
                table: "OP_CHATS",
                column: "OP_C_ANNOUNCESSHAREID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OP_CHATS_OP_C_ANNOUNCESSHAREID",
                table: "OP_CHATS");

            migrationBuilder.DropColumn(
                name: "OP_C_ANNOUNCESSHAREID",
                table: "OP_CHATS");
        }
    }
}
