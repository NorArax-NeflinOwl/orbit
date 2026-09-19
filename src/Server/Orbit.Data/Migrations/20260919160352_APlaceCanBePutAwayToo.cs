using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Data.Migrations
{
    /// <inheritdoc />
    public partial class APlaceCanBePutAwayToo : Migration
    {
        /// <summary>
        /// The fifth kind of thing that can be put away rather than thrown away - see
        /// ThingsCanBePutAwayRatherThanDeleted, which gave the other four the same column. A place was
        /// the one thing on the map with no way off it but deletion.
        ///
        /// False for everything stored, which is what it means: nothing could be archived before this,
        /// so nothing was.
        /// </summary>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "OP_P_ISARCHIVED",
                table: "OP_PLACES",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OP_P_ISARCHIVED",
                table: "OP_PLACES");
        }
    }
}
