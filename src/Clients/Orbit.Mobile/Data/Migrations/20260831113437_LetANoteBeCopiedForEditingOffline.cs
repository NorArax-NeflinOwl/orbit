using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Mobile.Data.Migrations
{
    /// <inheritdoc />
    public partial class LetANoteBeCopiedForEditingOffline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "CopiedAtUtc",
                table: "Notes",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CopyBaseContent",
                table: "Notes",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CopyBaseTitle",
                table: "Notes",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "CopyOfLocalId",
                table: "Notes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsKeptCopy",
                table: "Notes",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CopiedAtUtc",
                table: "Notes");

            migrationBuilder.DropColumn(
                name: "CopyBaseContent",
                table: "Notes");

            migrationBuilder.DropColumn(
                name: "CopyBaseTitle",
                table: "Notes");

            migrationBuilder.DropColumn(
                name: "CopyOfLocalId",
                table: "Notes");

            migrationBuilder.DropColumn(
                name: "IsKeptCopy",
                table: "Notes");
        }
    }
}
