using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationDismissalAndRetention : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RetentionDays",
                table: "NotificationSettings",
                type: "integer",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DismissedAtUtc",
                table: "NotificationEntries",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RetentionDays",
                table: "NotificationSettings");

            migrationBuilder.DropColumn(
                name: "DismissedAtUtc",
                table: "NotificationEntries");
        }
    }
}
