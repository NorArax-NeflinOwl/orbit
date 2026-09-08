using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Data.Migrations
{
    /// <inheritdoc />
    public partial class KeepADeletedMessageAsATombstone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "OP_C_DELETEDATUTC",
                table: "OP_CHATS",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OP_C_DELETEDBYUSERID",
                table: "OP_CHATS",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OP_C_DELETEDATUTC",
                table: "OP_CHATS");

            migrationBuilder.DropColumn(
                name: "OP_C_DELETEDBYUSERID",
                table: "OP_CHATS");
        }
    }
}
