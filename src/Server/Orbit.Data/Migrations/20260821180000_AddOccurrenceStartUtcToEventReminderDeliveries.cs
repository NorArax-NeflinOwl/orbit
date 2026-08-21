using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOccurrenceStartUtcToEventReminderDeliveries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EventReminderDeliveries_CalendarEventId_MinutesBeforeStart",
                table: "EventReminderDeliveries");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "OccurrenceStartUtc",
                table: "EventReminderDeliveries",
                type: "TEXT",
                nullable: false,
                defaultValue: DateTimeOffset.MinValue);

            // Every row that already exists predates recurring-event support, so each one is for a
            // non-recurring event whose only occurrence is the event's own StartUtc - backfill from there
            // instead of leaving the column at its default value.
            migrationBuilder.Sql(
                """
                UPDATE EventReminderDeliveries
                SET OccurrenceStartUtc = (
                    SELECT CalendarEvents.StartUtc
                    FROM CalendarEvents
                    WHERE CalendarEvents.Id = EventReminderDeliveries.CalendarEventId
                )
                WHERE EXISTS (
                    SELECT 1 FROM CalendarEvents WHERE CalendarEvents.Id = EventReminderDeliveries.CalendarEventId
                );
                """);

            migrationBuilder.CreateIndex(
                name: "IX_EventReminderDeliveries_CalendarEventId_MinutesBeforeStart_OccurrenceStartUtc",
                table: "EventReminderDeliveries",
                columns: new[] { "CalendarEventId", "MinutesBeforeStart", "OccurrenceStartUtc" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EventReminderDeliveries_CalendarEventId_MinutesBeforeStart_OccurrenceStartUtc",
                table: "EventReminderDeliveries");

            migrationBuilder.DropColumn(
                name: "OccurrenceStartUtc",
                table: "EventReminderDeliveries");

            migrationBuilder.CreateIndex(
                name: "IX_EventReminderDeliveries_CalendarEventId_MinutesBeforeStart",
                table: "EventReminderDeliveries",
                columns: new[] { "CalendarEventId", "MinutesBeforeStart" },
                unique: true);
        }
    }
}
