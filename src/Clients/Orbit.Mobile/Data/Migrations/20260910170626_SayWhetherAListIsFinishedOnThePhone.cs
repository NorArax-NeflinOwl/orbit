using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Mobile.Data.Migrations
{
    /// <inheritdoc />
    public partial class SayWhetherAListIsFinishedOnThePhone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Every list this phone already holds starts where every list starts: nobody has said, so
            // the entries decide - see Orbit.Core.Tasks.TaskListCompletion.
            migrationBuilder.AddColumn<string>(
                name: "Completion",
                table: "TaskLists",
                type: "TEXT",
                nullable: false,
                defaultValue: "FromTheEntries");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Completion",
                table: "TaskLists");
        }
    }
}
