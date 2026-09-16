using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Data.Migrations
{
    /// <inheritdoc />
    public partial class ThingsCanBePutAwayRatherThanDeleted : Migration
    {
        /// <summary>
        /// One column on each of the four kinds that can be put away - see
        /// Orbit.Core.Folders.BuiltInFolder.Archived. The only built-in folder with a column: which of
        /// the other three something is in follows from what it already is, and being put away is a
        /// decision somebody takes rather than something a thing becomes.
        ///
        /// False for everything stored, which is what it means: nothing could be archived before this,
        /// so nothing was. Nothing to backfill and nothing to say about it afterwards.
        /// </summary>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var (table, column) in new[]
            {
                ("OP_NOTES", "OP_N_ISARCHIVED"),
                ("OP_TASKS", "OP_T_ISARCHIVED"),
                ("OP_EVENTS", "OP_E_ISARCHIVED"),
                ("OP_INVENTORIES", "OP_I_ISARCHIVED")
            })
            {
                migrationBuilder.AddColumn<bool>(
                    name: column,
                    table: table,
                    type: "boolean",
                    nullable: false,
                    defaultValue: false);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var (table, column) in new[]
            {
                ("OP_NOTES", "OP_N_ISARCHIVED"),
                ("OP_TASKS", "OP_T_ISARCHIVED"),
                ("OP_EVENTS", "OP_E_ISARCHIVED"),
                ("OP_INVENTORIES", "OP_I_ISARCHIVED")
            })
            {
                migrationBuilder.DropColumn(name: column, table: table);
            }
        }
    }
}
