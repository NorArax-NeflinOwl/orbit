using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Data.Migrations
{
    /// <summary>
    /// Gives every folder the page it is a tab on - see Orbit.Core.Folders.FolderScope. Until now one
    /// row was a tab on the notes and on the task lists at once, so a folder made for one kind of thing
    /// sat empty on the other page.
    ///
    /// The backfill is the whole point of writing this by hand. A folder that held notes has to end up
    /// on the notes, one that held lists on the task lists - and a folder that held both is two folders
    /// now, so the second one is written here and the notes are moved into it. Left to the column
    /// default alone, every folder anybody had filed a note into would have become a task-list tab and
    /// those notes would have fallen back to Public with nothing saying why.
    /// </summary>
    public partial class FoldersBelongToOnePage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OP_F_SCOPE",
                table: "OP_FOLDERS",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Tasks");

            // A folder holding both kinds is split: the copy keeps the name and the owner, takes the
            // notes with it, and the original stays where the task lists already are. Both halves of
            // this run in one statement so the notes are never pointing at a folder that is not there -
            // a data-modifying CTE always runs to completion, whether or not the outer query reads it.
            migrationBuilder.Sql(
                """
                WITH "mixed" AS (
                    SELECT f."OP_F_ID" AS "OldId", gen_random_uuid() AS "NewId", f."OP_F_USERID" AS "UserId",
                           f."OP_F_NAME" AS "Name", f."OP_F_CREATEDATUTC" AS "CreatedAtUtc",
                           f."OP_F_UPDATEDATUTC" AS "UpdatedAtUtc"
                    FROM "OP_FOLDERS" f
                    WHERE EXISTS (SELECT 1 FROM "OP_NOTES" n WHERE n."OP_N_FOLDERID" = f."OP_F_ID")
                      AND EXISTS (SELECT 1 FROM "OP_TASKS" t WHERE t."OP_T_FOLDERID" = f."OP_F_ID")
                ), "copied" AS (
                    INSERT INTO "OP_FOLDERS" (
                        "OP_F_ID", "OP_F_USERID", "OP_F_NAME", "OP_F_SCOPE", "OP_F_CREATEDATUTC", "OP_F_UPDATEDATUTC")
                    SELECT "NewId", "UserId", "Name", 'Notes', "CreatedAtUtc", "UpdatedAtUtc" FROM "mixed"
                    RETURNING "OP_F_ID"
                )
                UPDATE "OP_NOTES" n SET "OP_N_FOLDERID" = m."NewId"
                FROM "mixed" m WHERE n."OP_N_FOLDERID" = m."OldId";
                """);

            // What is left holding notes holds nothing else, so the folder itself moves to the notes.
            migrationBuilder.Sql(
                """
                UPDATE "OP_FOLDERS" f SET "OP_F_SCOPE" = 'Notes'
                WHERE f."OP_F_SCOPE" = 'Tasks'
                  AND EXISTS (SELECT 1 FROM "OP_NOTES" n WHERE n."OP_N_FOLDERID" = f."OP_F_ID");
                """);
        }

        /// <summary>
        /// Dropping the column puts every folder back on both pages, which is where they were. The
        /// copies made above are left behind as ordinary folders - deleting them would take the notes
        /// filed into them since with them, and an empty tab is the cheaper of the two mistakes.
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OP_F_SCOPE",
                table: "OP_FOLDERS");
        }
    }
}
