using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Data.Migrations
{
    /// <inheritdoc />
    public partial class NarrowARestockListToTheRound : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "OL_IT_ONLYCHECKEDREGULARLY",
                table: "OL_INVENTORIES_TASKS",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "OL_IT_REMINDERNOTIFICATIONCHANNEL",
                table: "OL_INVENTORIES_TASKS",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "Push");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OL_IT_ONLYCHECKEDREGULARLY",
                table: "OL_INVENTORIES_TASKS");

            migrationBuilder.DropColumn(
                name: "OL_IT_REMINDERNOTIFICATIONCHANNEL",
                table: "OL_INVENTORIES_TASKS");
        }
    }
}
