using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Data.Migrations
{
    /// <inheritdoc />
    public partial class AProductEntryKeepsWhatItPutOnTheShelf : Migration
    {
        /// <summary>
        /// How each product entry stands with the shelf behind it - see Orbit.Core.Tasks.TaskItemStock,
        /// which is what ticking one and unticking it read to know whether its minimum is already on the
        /// shelf. Stored by name like every other enum here.
        ///
        /// "None" for everything already stored, which is what it means: nothing had ever put an amount
        /// on a shelf on an entry's behalf before this, so nothing is owed back. An entry the shelf had
        /// crossed off before today therefore reads as ticked by hand, and is left alone rather than
        /// reopened - the safe direction for a rule about taking somebody's tick away.
        /// </summary>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OP_TI_STOCK",
                table: "OP_TASKS_ITEMS",
                type: "text",
                nullable: false,
                defaultValue: "None");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OP_TI_STOCK",
                table: "OP_TASKS_ITEMS");
        }
    }
}
