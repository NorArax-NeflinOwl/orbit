using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddShareAccessLevelSharePermission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OriginalOwnerUserId",
                table: "TaskShares",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Existing rows predate the concept of a chain of re-shares entirely - every one of them is
            // a first-hop offer straight from the original owner, so OwnerUserId on the same row already
            // *is* the original owner. Backfilling from it here keeps
            // ShareTaskListCommandHandler/AcceptTaskListShareCommandHandler from treating every
            // pre-existing share as if it had been offered by Guid.Empty.
            migrationBuilder.Sql("UPDATE TaskShares SET OriginalOwnerUserId = OwnerUserId;");

            migrationBuilder.AddColumn<Guid>(
                name: "OriginalOwnerUserId",
                table: "Tasks",
                type: "TEXT",
                nullable: true);

            // Existing shared (IsShared = 1) task lists predate this column the same way TaskShares rows
            // do - SharedByUserName was captured from the one and only owner that could have shared it,
            // so that row's own UserId already is the original owner. Non-shared rows are left null, same
            // as any row created after this migration (TaskList.OriginalOwnerUserId is only meaningful
            // when IsShared is true).
            migrationBuilder.Sql("UPDATE Tasks SET OriginalOwnerUserId = UserId WHERE IsShared = 1;");

            migrationBuilder.AddColumn<Guid>(
                name: "OriginalOwnerUserId",
                table: "NoteShares",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.Sql("UPDATE NoteShares SET OriginalOwnerUserId = OwnerUserId;");

            migrationBuilder.AddColumn<Guid>(
                name: "OriginalOwnerUserId",
                table: "Notes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.Sql("UPDATE Notes SET OriginalOwnerUserId = UserId WHERE IsShared = 1;");

            migrationBuilder.AddColumn<Guid>(
                name: "OriginalOwnerUserId",
                table: "CalendarEventShares",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.Sql("UPDATE CalendarEventShares SET OriginalOwnerUserId = OwnerUserId;");

            migrationBuilder.AddColumn<Guid>(
                name: "OriginalOwnerUserId",
                table: "CalendarEvents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.Sql("UPDATE CalendarEvents SET OriginalOwnerUserId = UserId WHERE IsShared = 1;");

            migrationBuilder.CreateIndex(
                name: "IX_TaskShares_SourceTaskListId_RecipientUserId",
                table: "TaskShares",
                columns: new[] { "SourceTaskListId", "RecipientUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_NoteShares_SourceNoteId_RecipientUserId",
                table: "NoteShares",
                columns: new[] { "SourceNoteId", "RecipientUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEventShares_SourceCalendarEventId_RecipientUserId",
                table: "CalendarEventShares",
                columns: new[] { "SourceCalendarEventId", "RecipientUserId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TaskShares_SourceTaskListId_RecipientUserId",
                table: "TaskShares");

            migrationBuilder.DropIndex(
                name: "IX_NoteShares_SourceNoteId_RecipientUserId",
                table: "NoteShares");

            migrationBuilder.DropIndex(
                name: "IX_CalendarEventShares_SourceCalendarEventId_RecipientUserId",
                table: "CalendarEventShares");

            migrationBuilder.DropColumn(
                name: "OriginalOwnerUserId",
                table: "TaskShares");

            migrationBuilder.DropColumn(
                name: "OriginalOwnerUserId",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "OriginalOwnerUserId",
                table: "NoteShares");

            migrationBuilder.DropColumn(
                name: "OriginalOwnerUserId",
                table: "Notes");

            migrationBuilder.DropColumn(
                name: "OriginalOwnerUserId",
                table: "CalendarEventShares");

            migrationBuilder.DropColumn(
                name: "OriginalOwnerUserId",
                table: "CalendarEvents");
        }
    }
}
