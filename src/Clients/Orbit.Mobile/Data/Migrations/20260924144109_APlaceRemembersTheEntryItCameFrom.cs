using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Mobile.Data.Migrations
{
    /// <inheritdoc />
    public partial class APlaceRemembersTheEntryItCameFrom : Migration
    {
        /// <summary>
        /// Which Location entry of a task list a place was made from - see LocalPlace.SourceTaskItemId,
        /// and Orbit.Web's TaskEntryPlaces, which is what makes them. The server has held this since
        /// 2026-09-11 and the phone threw it away on every sync, so a place made from an entry was
        /// indistinguishable here from one somebody kept by hand.
        ///
        /// Null for everything already stored, which is what it means: the phone was never told. It
        /// arrives from the server on the next sync, and nothing here ever sends it back - see
        /// SavePlaceRequest.SourceTaskItemId, where null means "leave the entry alone".
        /// </summary>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SourceTaskItemId",
                table: "Places",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SourceTaskItemId",
                table: "Places");
        }
    }
}
