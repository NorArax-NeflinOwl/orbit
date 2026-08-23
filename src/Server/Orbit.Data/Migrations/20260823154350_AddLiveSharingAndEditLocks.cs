using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLiveSharingAndEditLocks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OriginalOwnerUserId",
                table: "TaskShares");

            migrationBuilder.DropColumn(
                name: "SharedTaskListId",
                table: "TaskShares");

            migrationBuilder.DropColumn(
                name: "AccessLevel",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "IsShared",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "OriginalOwnerUserId",
                table: "NoteShares");

            migrationBuilder.DropColumn(
                name: "SharedNoteId",
                table: "NoteShares");

            migrationBuilder.DropColumn(
                name: "AccessLevel",
                table: "Notes");

            migrationBuilder.DropColumn(
                name: "IsShared",
                table: "Notes");

            migrationBuilder.DropColumn(
                name: "OriginalOwnerUserId",
                table: "CalendarEventShares");

            migrationBuilder.DropColumn(
                name: "SharedCalendarEventId",
                table: "CalendarEventShares");

            migrationBuilder.DropColumn(
                name: "AccessLevel",
                table: "CalendarEvents");

            migrationBuilder.DropColumn(
                name: "IsShared",
                table: "CalendarEvents");

            migrationBuilder.RenameColumn(
                name: "SharedByUserName",
                table: "Tasks",
                newName: "LockedByUserName");

            migrationBuilder.RenameColumn(
                name: "OriginalOwnerUserId",
                table: "Tasks",
                newName: "LockedByUserId");

            migrationBuilder.RenameColumn(
                name: "SharedByUserName",
                table: "Notes",
                newName: "LockedByUserName");

            migrationBuilder.RenameColumn(
                name: "OriginalOwnerUserId",
                table: "Notes",
                newName: "LockedByUserId");

            migrationBuilder.RenameColumn(
                name: "SharedByUserName",
                table: "CalendarEvents",
                newName: "LockedByUserName");

            migrationBuilder.RenameColumn(
                name: "OriginalOwnerUserId",
                table: "CalendarEvents",
                newName: "LockedByUserId");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LockExpiresAtUtc",
                table: "Tasks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LockExpiresAtUtc",
                table: "Notes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LockExpiresAtUtc",
                table: "CalendarEvents",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LockExpiresAtUtc",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "LockExpiresAtUtc",
                table: "Notes");

            migrationBuilder.DropColumn(
                name: "LockExpiresAtUtc",
                table: "CalendarEvents");

            migrationBuilder.RenameColumn(
                name: "LockedByUserName",
                table: "Tasks",
                newName: "SharedByUserName");

            migrationBuilder.RenameColumn(
                name: "LockedByUserId",
                table: "Tasks",
                newName: "OriginalOwnerUserId");

            migrationBuilder.RenameColumn(
                name: "LockedByUserName",
                table: "Notes",
                newName: "SharedByUserName");

            migrationBuilder.RenameColumn(
                name: "LockedByUserId",
                table: "Notes",
                newName: "OriginalOwnerUserId");

            migrationBuilder.RenameColumn(
                name: "LockedByUserName",
                table: "CalendarEvents",
                newName: "SharedByUserName");

            migrationBuilder.RenameColumn(
                name: "LockedByUserId",
                table: "CalendarEvents",
                newName: "OriginalOwnerUserId");

            migrationBuilder.AddColumn<Guid>(
                name: "OriginalOwnerUserId",
                table: "TaskShares",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SharedTaskListId",
                table: "TaskShares",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AccessLevel",
                table: "Tasks",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsShared",
                table: "Tasks",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "OriginalOwnerUserId",
                table: "NoteShares",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SharedNoteId",
                table: "NoteShares",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AccessLevel",
                table: "Notes",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsShared",
                table: "Notes",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "OriginalOwnerUserId",
                table: "CalendarEventShares",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "SharedCalendarEventId",
                table: "CalendarEventShares",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AccessLevel",
                table: "CalendarEvents",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsShared",
                table: "CalendarEvents",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }
    }
}
