using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Data.Migrations
{
    /// <inheritdoc />
    public partial class AListThatGathersListsSaysSo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // No column: OP_T_ISGROUP has been there since the beginning. What changed on 2026-09-15 is
            // that the domain settles it rather than taking the caller's word for it - a list with an
            // entry pointing at another list is a group list, whatever a box says (see
            // Orbit.Core.Tasks.TaskList.IsGroup).
            //
            // So what is stored is brought into line here, rather than being left to change on whatever
            // save happens to come next. The same care EntryStandsForAnyOfItsLists took, and for the same
            // reason: somebody would otherwise find a list drawing its members with nothing in the
            // history to say when that started.
            //
            // Only lists that actually gather something. A list with no such entry keeps whatever answer
            // it was given, which is still the reader's to change.
            migrationBuilder.Sql(
                """
                UPDATE "OP_TASKS"
                SET "OP_T_ISGROUP" = true
                WHERE "OP_T_ISGROUP" = false
                  AND "OP_T_ID" IN (
                      SELECT "OP_TI_TASKID"
                      FROM "OP_TASKS_ITEMS"
                      WHERE "OP_TI_ID" IN (SELECT "OL_TI_TASKITEMID" FROM "OL_TASKS_ITEMS"));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Nothing to undo. Which lists were ticked here and which were already ticked is not written
            // down anywhere, so turning them all off would take away answers readers had given
            // themselves - and a down migration that loses more than the up added is worse than one that
            // does nothing. The column is untouched either way.
        }
    }
}
