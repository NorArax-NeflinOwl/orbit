using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Mobile.Data.Migrations
{
    /// <summary>
    /// Gives the four kinds on the phone the flag that says their owner has put them away - see
    /// Orbit.Core.Folders.BuiltInFolder.Archived, the tab they gather under while they are away. The one
    /// built-in folder this phone stores, because nothing else about a row could say it.
    ///
    /// False for everything already held, which is what it means: nothing could be archived before this,
    /// so nothing was. Nothing to backfill.
    /// </summary>
    public partial class PutThingsAwayOnThePhone : Migration
    {
        private static readonly string[] Tables = ["Notes", "TaskLists", "CalendarEvents", "Inventories"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var table in Tables)
            {
                migrationBuilder.AddColumn<bool>(
                    name: "IsArchived",
                    table: table,
                    type: "INTEGER",
                    nullable: false,
                    defaultValue: false);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in Tables)
            {
                migrationBuilder.DropColumn(name: "IsArchived", table: table);
            }
        }
    }
}
