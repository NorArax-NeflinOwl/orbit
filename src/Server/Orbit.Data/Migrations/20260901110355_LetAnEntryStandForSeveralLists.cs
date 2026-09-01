using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Data.Migrations
{
    /// <summary>
    /// Moves an entry's link to another task list out of a column and into a table, so an entry can
    /// stand for several lists rather than one - see Orbit.Core.Tasks.TaskItem.LinkedTaskListIds.
    ///
    /// The order matters: the table is made and the existing links are copied into it before the old
    /// column is dropped. Scaffolding put the drop first, which would have thrown every existing link
    /// away. Down reverses it and keeps the first link of each entry - an entry naming three lists
    /// comes back naming one, because a column has room for no more than that.
    /// </summary>
    public partial class LetAnEntryStandForSeveralLists : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TaskItemTaskListLinkEntity",
                columns: table => new
                {
                    TaskItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    LinkedTaskListId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskItemTaskListLinkEntity", x => new { x.TaskItemId, x.LinkedTaskListId });
                    table.ForeignKey(
                        name: "FK_TaskItemTaskListLinkEntity_TaskItemEntity_TaskItemId",
                        column: x => x.TaskItemId,
                        principalTable: "TaskItemEntity",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TaskItemTaskListLinkEntity_LinkedTaskListId",
                table: "TaskItemTaskListLinkEntity",
                column: "LinkedTaskListId");

            // Every link that already exists, kept. Position 0 because each entry had exactly one.
            migrationBuilder.Sql("""
                INSERT INTO "TaskItemTaskListLinkEntity" ("TaskItemId", "LinkedTaskListId", "Position")
                SELECT "Id", "LinkedTaskListId", 0
                FROM "TaskItemEntity"
                WHERE "LinkedTaskListId" IS NOT NULL;
                """);

            migrationBuilder.DropColumn(
                name: "LinkedTaskListId",
                table: "TaskItemEntity");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "LinkedTaskListId",
                table: "TaskItemEntity",
                type: "uuid",
                nullable: true);

            // The first link of each entry. Anything beyond the first is lost, which is what going back
            // to a column means.
            migrationBuilder.Sql("""
                UPDATE "TaskItemEntity" AS item
                SET "LinkedTaskListId" = link."LinkedTaskListId"
                FROM (
                    SELECT DISTINCT ON ("TaskItemId") "TaskItemId", "LinkedTaskListId"
                    FROM "TaskItemTaskListLinkEntity"
                    ORDER BY "TaskItemId", "Position"
                ) AS link
                WHERE link."TaskItemId" = item."Id";
                """);

            migrationBuilder.DropTable(
                name: "TaskItemTaskListLinkEntity");
        }
    }
}
