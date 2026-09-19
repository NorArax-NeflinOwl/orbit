using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Mobile.Data.Migrations
{
    /// <inheritdoc />
    public partial class AnAnswerAboutASharedShelfRow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The reader's answer about a shelf row several of their lists ask for, carried with the
            // queued change until the synchroniser sends it - see LocalInventory.SplitEvenlyAcross.
            // Empty is what every row already stored means, and what a save that asked nothing means.
            migrationBuilder.AddColumn<string>(
                name: "SplitEvenlyAcross",
                table: "Inventories",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SplitEvenlyAcross",
                table: "Inventories");
        }
    }
}
