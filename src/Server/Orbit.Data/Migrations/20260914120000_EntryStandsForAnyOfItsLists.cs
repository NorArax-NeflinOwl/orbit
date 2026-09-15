using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Data.Migrations
{
    /// <inheritdoc />
    public partial class EntryStandsForAnyOfItsLists : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "OP_TI_NEEDSEVERYLINKEDLIST",
                table: "OP_TASKS_ITEMS",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // The default is the new rule - any one of the lists an entry stands for is enough - and it
            // is the opposite of what every entry stored before this column existed meant. So each of
            // those is marked: an entry that already points at a list keeps meaning "all of them", and
            // nothing anybody saved changes what it says on the day the default does.
            //
            // Only the entries that point at something. An entry standing for no list has no rule to
            // keep, and marking it would leave the table saying something about entries the rule cannot
            // be read off.
            migrationBuilder.Sql(
                """
                UPDATE "OP_TASKS_ITEMS"
                SET "OP_TI_NEEDSEVERYLINKEDLIST" = true
                WHERE "OP_TI_ID" IN (SELECT "OL_TI_TASKITEMID" FROM "OL_TASKS_ITEMS");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OP_TI_NEEDSEVERYLINKEDLIST",
                table: "OP_TASKS_ITEMS");
        }
    }
}
