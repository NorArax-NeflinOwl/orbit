using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Data.Migrations
{
    /// <inheritdoc />
    public partial class PinASharedThingForItsRecipient : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "OP_TS_ISPINNEDBYRECIPIENT",
                table: "OP_TASKS_SHARED",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "OP_NS_ISPINNEDBYRECIPIENT",
                table: "OP_NOTES_SHARED",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OP_TS_ISPINNEDBYRECIPIENT",
                table: "OP_TASKS_SHARED");

            migrationBuilder.DropColumn(
                name: "OP_NS_ISPINNEDBYRECIPIENT",
                table: "OP_NOTES_SHARED");
        }
    }
}
