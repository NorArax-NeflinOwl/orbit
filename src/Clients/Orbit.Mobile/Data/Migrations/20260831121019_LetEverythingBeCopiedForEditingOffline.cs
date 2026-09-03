using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Mobile.Data.Migrations
{
    /// <inheritdoc />
    public partial class LetEverythingBeCopiedForEditingOffline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CopyBaseContent",
                table: "Notes",
                newName: "CopyBaseLines");

            // The column now holds rendered lines rather than a note's own line objects, so what is in
            // it no longer parses. Emptied rather than converted: it is only ever set on a copy still
            // awaiting review, this shape never left a development phone, and an empty snapshot makes a
            // review read as "all of this is new" rather than fail to open.
            // Every note, not only the copies: the column before this one was added with an empty-string
            // default too, so rows that never held a snapshot are blank rather than empty JSON.
            migrationBuilder.Sql("UPDATE \"Notes\" SET \"CopyBaseLines\" = '[]'");

            migrationBuilder.AddColumn<long>(
                name: "CopiedAtUtc",
                table: "Warehouses",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CopyBaseLines",
                table: "Warehouses",
                type: "TEXT",
                nullable: false,
                // An empty list, not an empty string: what goes in here is JSON, and every existing row
                // is backfilled with this.
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "CopyBaseTitle",
                table: "Warehouses",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "CopyOfLocalId",
                table: "Warehouses",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsKeptCopy",
                table: "Warehouses",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "CopiedAtUtc",
                table: "TaskLists",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CopyBaseLines",
                table: "TaskLists",
                type: "TEXT",
                nullable: false,
                // An empty list, not an empty string: what goes in here is JSON, and every existing row
                // is backfilled with this.
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "CopyBaseTitle",
                table: "TaskLists",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "CopyOfLocalId",
                table: "TaskLists",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsKeptCopy",
                table: "TaskLists",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "CopiedAtUtc",
                table: "CalendarEvents",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CopyBaseLines",
                table: "CalendarEvents",
                type: "TEXT",
                nullable: false,
                // An empty list, not an empty string: what goes in here is JSON, and every existing row
                // is backfilled with this.
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "CopyBaseTitle",
                table: "CalendarEvents",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "CopyOfLocalId",
                table: "CalendarEvents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsKeptCopy",
                table: "CalendarEvents",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CopiedAtUtc",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "CopyBaseLines",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "CopyBaseTitle",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "CopyOfLocalId",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "IsKeptCopy",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "CopiedAtUtc",
                table: "TaskLists");

            migrationBuilder.DropColumn(
                name: "CopyBaseLines",
                table: "TaskLists");

            migrationBuilder.DropColumn(
                name: "CopyBaseTitle",
                table: "TaskLists");

            migrationBuilder.DropColumn(
                name: "CopyOfLocalId",
                table: "TaskLists");

            migrationBuilder.DropColumn(
                name: "IsKeptCopy",
                table: "TaskLists");

            migrationBuilder.DropColumn(
                name: "CopiedAtUtc",
                table: "CalendarEvents");

            migrationBuilder.DropColumn(
                name: "CopyBaseLines",
                table: "CalendarEvents");

            migrationBuilder.DropColumn(
                name: "CopyBaseTitle",
                table: "CalendarEvents");

            migrationBuilder.DropColumn(
                name: "CopyOfLocalId",
                table: "CalendarEvents");

            migrationBuilder.DropColumn(
                name: "IsKeptCopy",
                table: "CalendarEvents");

            migrationBuilder.RenameColumn(
                name: "CopyBaseLines",
                table: "Notes",
                newName: "CopyBaseContent");
        }
    }
}
