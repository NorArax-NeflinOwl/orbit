using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Data.Migrations
{
    /// <summary>
    /// Two things the event form asks for that the table had nowhere to keep: a notification at the
    /// moment the event begins, and a limit on how many times a repeating event repeats.
    ///
    /// Both add columns and neither rewrites anything, so Up on a live table is safe. An existing event
    /// says false and null - no starting notification, and a repeat rule limited only by its end date,
    /// which is what every existing event already meant. Down drops both and loses whatever was set.
    ///
    /// "Yearly" needed nothing here: the frequency is stored by name in a text column.
    /// </summary>
    public partial class SayWhenItStartsAndHowOftenItRepeats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "NotifyAtStart",
                table: "CalendarEvents",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "RecurrenceOccurrenceCount",
                table: "CalendarEvents",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NotifyAtStart",
                table: "CalendarEvents");

            migrationBuilder.DropColumn(
                name: "RecurrenceOccurrenceCount",
                table: "CalendarEvents");
        }
    }
}
