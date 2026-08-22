using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNoteContentJson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Content",
                table: "Notes",
                newName: "ContentJson");

            // The renamed column still holds the old plain-text content, not JSON - wrap each
            // non-empty value into a single-line NoteContentLine array so NoteRepository's
            // JsonSerializer.Deserialize<List<NoteContentLine>> can read pre-existing notes.
            migrationBuilder.Sql(
                """
                UPDATE Notes
                SET ContentJson = json_array(json_object('Text', ContentJson, 'IsChecklistItem', json('false'), 'IsChecked', json('false')))
                WHERE ContentJson IS NOT NULL AND ContentJson != '';
                """);

            migrationBuilder.Sql(
                """
                UPDATE Notes
                SET ContentJson = '[]'
                WHERE ContentJson IS NULL OR ContentJson = '';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Best-effort: collapse back to plain text by taking the first line's text, since the
            // checklist structure itself (multiple lines, checked state) has no home in a string column.
            migrationBuilder.Sql(
                """
                UPDATE Notes
                SET ContentJson = COALESCE((SELECT value ->> 'Text' FROM json_each(ContentJson) LIMIT 1), '')
                WHERE ContentJson IS NOT NULL;
                """);

            migrationBuilder.RenameColumn(
                name: "ContentJson",
                table: "Notes",
                newName: "Content");
        }
    }
}
