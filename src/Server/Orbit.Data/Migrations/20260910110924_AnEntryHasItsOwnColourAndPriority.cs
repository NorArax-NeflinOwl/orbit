using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Data.Migrations
{
    /// <inheritdoc />
    public partial class AnEntryHasItsOwnColourAndPriority : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OP_TI_COLOUR",
                table: "OP_TASKS_ITEMS",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OP_TI_PRIORITY",
                table: "OP_TASKS_ITEMS",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Normal");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OP_TI_COLOUR",
                table: "OP_TASKS_ITEMS");

            migrationBuilder.DropColumn(
                name: "OP_TI_PRIORITY",
                table: "OP_TASKS_ITEMS");
        }
    }
}
