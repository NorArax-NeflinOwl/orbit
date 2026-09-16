using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Mobile.Data.Migrations
{
    /// <summary>
    /// Gives an event and an inventory on the phone the folder their owner filed them under, the column
    /// a note and a task list have had since FileNotesAndListsInFoldersOnThePhone. Null for everything
    /// already held, which is what "filed nowhere" is - see Orbit.Core.Folders.FolderPlacement - so
    /// nothing needs backfilling. The id is one of this phone's own, as a note's is: it is translated
    /// to the server's on the way out and back on the way in (see CalendarEventSynchronizer).
    /// </summary>
    public partial class FileEventsAndShelvesInFoldersOnThePhone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FolderId",
                table: "CalendarEvents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FolderId",
                table: "Inventories",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FolderId",
                table: "CalendarEvents");

            migrationBuilder.DropColumn(
                name: "FolderId",
                table: "Inventories");
        }
    }
}
