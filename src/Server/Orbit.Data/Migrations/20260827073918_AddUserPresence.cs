using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserPresence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PresenceAvailability",
                table: "Users",
                type: "text",
                nullable: false,
                // Accounts that predate presence chose nothing, which is the same as choosing to be
                // available; whether they show as here at all is decided by PresenceLastSeenAtUtc,
                // which stays null until they next open the app.
                defaultValue: "Available");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PresenceLastSeenAtUtc",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PresenceAvailability",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PresenceLastSeenAtUtc",
                table: "Users");
        }
    }
}
