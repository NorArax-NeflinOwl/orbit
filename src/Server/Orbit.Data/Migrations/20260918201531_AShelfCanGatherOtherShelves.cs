using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Data.Migrations
{
    /// <inheritdoc />
    public partial class AShelfCanGatherOtherShelves : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Which shelves a group shelf gathers - see Orbit.Core.Inventories.Inventory.GathersInventoryIds,
            // asked for on 2026-09-18: one entry on the list of inventories holding smaller inventories
            // inside it, each able to answer to a different task list.
            //
            // A table rather than a column, because a group gathers as many as somebody arranged and the
            // order is theirs - the same shape OL_TASKS_ITEMS has for the lists an entry stands for, and
            // no foreign key either end for the same reason: a member that has been deleted reads as
            // "nothing there" when the group is walked, and a constraint would instead refuse the delete
            // or take the group's own row with it. InventoryRepository.DeleteAsync clears both ends.
            migrationBuilder.CreateTable(
                name: "OL_INVENTORIES_GATHERED",
                columns: table => new
                {
                    OL_IG_INVENTORYID = table.Column<Guid>(type: "uuid", nullable: false),
                    OL_IG_GATHEREDINVENTORYID = table.Column<Guid>(type: "uuid", nullable: false),
                    OL_IG_POSITION = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OL_INVENTORIES_GATHERED", x => new { x.OL_IG_INVENTORYID, x.OL_IG_GATHEREDINVENTORYID });
                });

            migrationBuilder.CreateIndex(
                name: "IX_OL_INVENTORIES_GATHERED_OL_IG_GATHEREDINVENTORYID",
                table: "OL_INVENTORIES_GATHERED",
                column: "OL_IG_GATHEREDINVENTORYID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OL_INVENTORIES_GATHERED");
        }
    }
}
