using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Data.Migrations
{
    /// <summary>
    /// Gives an event and an inventory the folder their owner filed them under, the column a note and a
    /// task list have had since FileNotesAndListsInFolders. Null for everything already stored, which is
    /// what "filed nowhere" is - a built-in folder rather than none at all, worked out from the row
    /// itself (Orbit.Core.Folders.FolderPlacement), so nothing needs backfilling.
    ///
    /// The folders themselves need no migration: their scope is stored by name and the two new ones are
    /// simply names no row uses yet - see Orbit.Core.Folders.FolderScope.
    /// </summary>
    public partial class EventsAndShelvesAreFiledInFolders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OP_E_FOLDERID",
                table: "OP_EVENTS",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OP_I_FOLDERID",
                table: "OP_INVENTORIES",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OP_E_FOLDERID",
                table: "OP_EVENTS");

            migrationBuilder.DropColumn(
                name: "OP_I_FOLDERID",
                table: "OP_INVENTORIES");
        }
    }
}
