using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Mobile.Data.Migrations
{
    /// <inheritdoc />
    public partial class RememberHowMuchANoteMatters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // "Normal" rather than the empty string EF defaults to: a note already on the phone has a
            // priority, it is just not written down yet, and an empty one would be sent to a server
            // that reads this as an ItemPriority by name.
            migrationBuilder.AddColumn<string>(
                name: "Priority",
                table: "Notes",
                type: "TEXT",
                nullable: false,
                defaultValue: "Normal");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Priority",
                table: "Notes");
        }
    }
}
