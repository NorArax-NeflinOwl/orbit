using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryItemUnit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // "Piece" rather than the empty string EF fills in by default: everything already on a shelf
            // was counted one by one, and an empty unit is one Enum.Parse cannot read back.
            migrationBuilder.AddColumn<string>(
                name: "Unit",
                table: "InventoryItems",
                type: "text",
                nullable: false,
                defaultValue: "Piece");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Unit",
                table: "InventoryItems");
        }
    }
}
