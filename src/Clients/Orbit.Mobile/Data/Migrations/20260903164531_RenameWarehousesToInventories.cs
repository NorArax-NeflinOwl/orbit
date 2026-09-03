using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Mobile.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenameWarehousesToInventories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Written by hand as renames. EF scaffolds a drop-and-create here, because renaming the
            // entity (LocalWarehouse -> LocalInventory) leaves its differ nothing to match the old
            // table against - and that would throw away anything the phone made offline and has not
            // sent yet, which is exactly what the local store exists to hold on to.
            migrationBuilder.RenameColumn(
                name: "LinkedWarehouseId",
                table: "TaskLists",
                newName: "LinkedInventoryId");

            migrationBuilder.RenameTable(
                name: "Warehouses",
                newName: "Inventories");

            migrationBuilder.RenameIndex(
                name: "IX_Warehouses_ServerId",
                table: "Inventories",
                newName: "IX_Inventories_ServerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "IX_Inventories_ServerId",
                table: "Inventories",
                newName: "IX_Warehouses_ServerId");

            migrationBuilder.RenameTable(
                name: "Inventories",
                newName: "Warehouses");

            migrationBuilder.RenameColumn(
                name: "LinkedInventoryId",
                table: "TaskLists",
                newName: "LinkedWarehouseId");
        }
    }
}
