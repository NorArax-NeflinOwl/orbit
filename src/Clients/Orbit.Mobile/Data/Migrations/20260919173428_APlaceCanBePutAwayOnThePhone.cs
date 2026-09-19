using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Mobile.Data.Migrations
{
    /// <inheritdoc />
    public partial class APlaceCanBePutAwayOnThePhone : Migration
    {
        /// <summary>
        /// The fifth kind on this phone that can be put away rather than thrown away - see
        /// PutThingsAwayOnThePhone, which gave the other four the same column, and
        /// Orbit.Core.Places.Place.IsArchived on the server's own half.
        ///
        /// False for everything already held, which is what it means: nothing about a place could say
        /// it before this, so nothing did. The flag arrives from the server on the next sync, so a place
        /// put away in a browser leaves this phone's list the first time it hears about it.
        /// </summary>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "Places",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "Places");
        }
    }
}
