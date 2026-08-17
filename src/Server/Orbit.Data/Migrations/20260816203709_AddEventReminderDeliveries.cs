using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEventReminderDeliveries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EventReminderDeliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CalendarEventId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MinutesBeforeStart = table.Column<int>(type: "INTEGER", nullable: false),
                    SentAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventReminderDeliveries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EventReminderDeliveries_CalendarEventId_MinutesBeforeStart",
                table: "EventReminderDeliveries",
                columns: new[] { "CalendarEventId", "MinutesBeforeStart" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EventReminderDeliveries");
        }
    }
}
