using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Data.Migrations
{
    /// <summary>
    /// Replaces the "the reader marked this finished" flag with the three answers there actually are -
    /// see Orbit.Core.Tasks.TaskListCompletion. The boolean could not say the ordinary case where every
    /// entry is ticked off and the list itself is not done yet: unticking the box handed the question
    /// back to the entries, which said "finished" again and put the tick straight back.
    ///
    /// The column is added, filled from the old one, and only then is the old one dropped - in that
    /// order, so nobody's closed list is reopened by the upgrade. `true` becomes Finished; `false` was
    /// only ever "nobody has said", which is FromTheEntries and is the column's default.
    /// </summary>
    public partial class ATaskListCanBeUnfinishedWithEverythingTickedOff : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OP_T_COMPLETION",
                table: "OP_TASKS",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "FromTheEntries");

            migrationBuilder.Sql(
                """
                UPDATE "OP_TASKS" SET "OP_T_COMPLETION" = 'Finished' WHERE "OP_T_ISMARKEDCOMPLETED";
                """);

            migrationBuilder.DropColumn(
                name: "OP_T_ISMARKEDCOMPLETED",
                table: "OP_TASKS");
        }

        /// <summary>
        /// Going back loses the difference the new column exists for: a list its owner said is
        /// <em>not</em> finished with everything ticked off becomes an ordinary finished-looking list
        /// again, because that is the only thing the boolean can say about it.
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "OP_T_ISMARKEDCOMPLETED",
                table: "OP_TASKS",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(
                """
                UPDATE "OP_TASKS" SET "OP_T_ISMARKEDCOMPLETED" = true WHERE "OP_T_COMPLETION" = 'Finished';
                """);

            migrationBuilder.DropColumn(
                name: "OP_T_COMPLETION",
                table: "OP_TASKS");
        }
    }
}
