using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Data.Migrations
{
    /// <summary>
    /// Gives a place the folder its owner filed it under, the column the other four kinds have had since
    /// FileNotesAndListsInFolders and EventsAndShelvesAreFiledInFolders. The map is the fifth and last
    /// kind that can be filed, asked for with the Setup page on 2026-09-24 ("folders per notes, tasks,
    /// events, inventories and the map").
    ///
    /// Null for everything already stored, which is what "filed nowhere" is - a built-in folder rather
    /// than none at all, worked out from the row itself (Orbit.Core.Folders.FolderPlacement), so nothing
    /// needs backfilling. Readable rather than sealed on a private place, which most places are: it names
    /// a folder of the owner's own and says nothing about where the place is.
    ///
    /// The folders themselves need no migration: their scope is stored by name and Places is simply a
    /// name no row uses yet - see Orbit.Core.Folders.FolderScope.
    /// </summary>
    public partial class PlacesAreFiledInFolders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OP_P_FOLDERID",
                table: "OP_PLACES",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OP_P_FOLDERID",
                table: "OP_PLACES");
        }
    }
}
