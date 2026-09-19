using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Mobile.Data.Migrations
{
    /// <inheritdoc />
    public partial class AShelfGathersOtherShelvesOnThePhone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Which shelves a group shelf gathers, by the ids the server knows them by - see
            // LocalInventory.GathersServerIds. Empty is what every row already stored means, and what a
            // server that has not learned about gathering answers: an ordinary shelf gathers nothing.
            migrationBuilder.AddColumn<string>(
                name: "GathersServerIds",
                table: "Inventories",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GathersServerIds",
                table: "Inventories");
        }
    }
}
