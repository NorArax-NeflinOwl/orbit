using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Data.Migrations
{
    /// <inheritdoc />
    public partial class MoveCalendarKindOntoTaskItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Kind",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "Location",
                table: "Tasks");

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "TaskItemEntity",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Checklist");

            migrationBuilder.AddColumn<Guid>(
                name: "LinkedCalendarEventId",
                table: "TaskItemEntity",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Location",
                table: "TaskItemEntity",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Kind",
                table: "TaskItemEntity");

            migrationBuilder.DropColumn(
                name: "LinkedCalendarEventId",
                table: "TaskItemEntity");

            migrationBuilder.DropColumn(
                name: "Location",
                table: "TaskItemEntity");

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "Tasks",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Checklist");

            migrationBuilder.AddColumn<string>(
                name: "Location",
                table: "Tasks",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");
        }
    }
}
