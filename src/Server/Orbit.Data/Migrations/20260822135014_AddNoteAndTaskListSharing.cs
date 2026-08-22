using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNoteAndTaskListSharing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccessLevel",
                table: "Tasks",
                type: "TEXT",
                nullable: false,
                // Existing rows predate the concept of an access level entirely - "ReadOnly" matches the
                // only behavior that ever existed before this migration (a shared copy was always
                // read-only), so backfilling it here keeps Enum.Parse<ShareAccessLevel> in the domain
                // layer from throwing on rows written before this column existed.
                defaultValue: "ReadOnly");

            migrationBuilder.AddColumn<bool>(
                name: "IsShared",
                table: "Tasks",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SharedByUserName",
                table: "Tasks",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AccessLevel",
                table: "Notes",
                type: "TEXT",
                nullable: false,
                // Existing rows predate the concept of an access level entirely - "ReadOnly" matches the
                // only behavior that ever existed before this migration (a shared copy was always
                // read-only), so backfilling it here keeps Enum.Parse<ShareAccessLevel> in the domain
                // layer from throwing on rows written before this column existed.
                defaultValue: "ReadOnly");

            migrationBuilder.AddColumn<bool>(
                name: "IsShared",
                table: "Notes",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SharedByUserName",
                table: "Notes",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AccessLevel",
                table: "CalendarEventShares",
                type: "TEXT",
                nullable: false,
                // Existing rows predate the concept of an access level entirely - "ReadOnly" matches the
                // only behavior that ever existed before this migration (a shared copy was always
                // read-only), so backfilling it here keeps Enum.Parse<ShareAccessLevel> in the domain
                // layer from throwing on rows written before this column existed.
                defaultValue: "ReadOnly");

            migrationBuilder.AddColumn<string>(
                name: "AccessLevel",
                table: "CalendarEvents",
                type: "TEXT",
                nullable: false,
                // Existing rows predate the concept of an access level entirely - "ReadOnly" matches the
                // only behavior that ever existed before this migration (a shared copy was always
                // read-only), so backfilling it here keeps Enum.Parse<ShareAccessLevel> in the domain
                // layer from throwing on rows written before this column existed.
                defaultValue: "ReadOnly");

            migrationBuilder.CreateTable(
                name: "NoteShares",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceNoteId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RecipientUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AccessLevel = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    AcceptedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    SharedNoteId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NoteShares", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TaskShares",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceTaskListId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RecipientUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AccessLevel = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    AcceptedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    SharedTaskListId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskShares", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NoteShares");

            migrationBuilder.DropTable(
                name: "TaskShares");

            migrationBuilder.DropColumn(
                name: "AccessLevel",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "IsShared",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "SharedByUserName",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "AccessLevel",
                table: "Notes");

            migrationBuilder.DropColumn(
                name: "IsShared",
                table: "Notes");

            migrationBuilder.DropColumn(
                name: "SharedByUserName",
                table: "Notes");

            migrationBuilder.DropColumn(
                name: "AccessLevel",
                table: "CalendarEventShares");

            migrationBuilder.DropColumn(
                name: "AccessLevel",
                table: "CalendarEvents");
        }
    }
}
