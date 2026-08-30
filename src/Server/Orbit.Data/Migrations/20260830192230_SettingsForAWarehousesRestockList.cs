using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Data.Migrations
{
    /// <inheritdoc />
    public partial class SettingsForAWarehousesRestockList : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "OnlyLinkedWithDueDate",
                table: "InventoryManagedTaskLists",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "RefreshTimeOfDayMinutes",
                table: "InventoryManagedTaskLists",
                type: "integer",
                nullable: false,
                defaultValue: 540);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OnlyLinkedWithDueDate",
                table: "InventoryManagedTaskLists");

            migrationBuilder.DropColumn(
                name: "RefreshTimeOfDayMinutes",
                table: "InventoryManagedTaskLists");
        }
    }
}
