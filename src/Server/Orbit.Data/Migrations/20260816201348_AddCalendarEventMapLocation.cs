using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCalendarEventMapLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Location",
                table: "CalendarEvents",
                newName: "LocationAddress");

            migrationBuilder.AddColumn<double>(
                name: "LocationLatitude",
                table: "CalendarEvents",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "LocationLongitude",
                table: "CalendarEvents",
                type: "REAL",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LocationLatitude",
                table: "CalendarEvents");

            migrationBuilder.DropColumn(
                name: "LocationLongitude",
                table: "CalendarEvents");

            migrationBuilder.RenameColumn(
                name: "LocationAddress",
                table: "CalendarEvents",
                newName: "Location");
        }
    }
}
