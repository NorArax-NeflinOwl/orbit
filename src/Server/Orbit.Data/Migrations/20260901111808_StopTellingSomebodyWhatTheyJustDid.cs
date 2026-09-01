using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Data.Migrations
{
    /// <summary>
    /// An event no longer announces itself to the person who just made it. That existed to prove the
    /// notification paths worked; the only thing worth saying when something is saved is said to
    /// somebody else, when it is shared with them.
    ///
    /// Dropping the column loses what each event had been set to, which is the point - nothing reads it
    /// any more. Down puts the column back defaulted to "None", so an older Orbit finds the field it
    /// expects and announces nothing, rather than announcing every event on a channel it did not pick.
    /// </summary>
    public partial class StopTellingSomebodyWhatTheyJustDid : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreationNotificationChannel",
                table: "CalendarEvents");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CreationNotificationChannel",
                table: "CalendarEvents",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "None");
        }
    }
}
