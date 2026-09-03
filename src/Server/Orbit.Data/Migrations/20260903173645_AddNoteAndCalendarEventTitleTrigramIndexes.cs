using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNoteAndCalendarEventTitleTrigramIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_notes_title_trgm",
                table: "OP_NOTES",
                column: "OP_N_TITLE")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_calendar_events_title_trgm",
                table: "OP_EVENTS",
                column: "OP_E_TITLE")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_notes_title_trgm",
                table: "OP_NOTES");

            migrationBuilder.DropIndex(
                name: "ix_calendar_events_title_trgm",
                table: "OP_EVENTS");
        }
    }
}
