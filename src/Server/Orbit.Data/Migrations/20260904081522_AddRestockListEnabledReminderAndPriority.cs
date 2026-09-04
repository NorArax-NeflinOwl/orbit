using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRestockListEnabledReminderAndPriority : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "OL_IT_ISENABLED",
                table: "OL_INVENTORIES_TASKS",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "OL_IT_LISTPRIORITY",
                table: "OL_INVENTORIES_TASKS",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "Normal");

            migrationBuilder.AddColumn<bool>(
                name: "OL_IT_REMINDDAILY",
                table: "OL_INVENTORIES_TASKS",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OL_IT_ISENABLED",
                table: "OL_INVENTORIES_TASKS");

            migrationBuilder.DropColumn(
                name: "OL_IT_LISTPRIORITY",
                table: "OL_INVENTORIES_TASKS");

            migrationBuilder.DropColumn(
                name: "OL_IT_REMINDDAILY",
                table: "OL_INVENTORIES_TASKS");
        }
    }
}
