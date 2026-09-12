using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Data.Migrations
{
    /// <inheritdoc />
    public partial class LetAnEntryBeTheSameThingAsAnother : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "OP_TI_CREATEDATUTC",
                table: "OP_TASKS_ITEMS",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OP_TI_REFERENCESTASKITEMID",
                table: "OP_TASKS_ITEMS",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OP_TI_REQUIREDQUANTITY",
                table: "OP_TASKS_ITEMS",
                type: "numeric",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OP_TASKS_ITEMS_OP_TI_REFERENCESTASKITEMID",
                table: "OP_TASKS_ITEMS",
                column: "OP_TI_REFERENCESTASKITEMID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OP_TASKS_ITEMS_OP_TI_REFERENCESTASKITEMID",
                table: "OP_TASKS_ITEMS");

            migrationBuilder.DropColumn(
                name: "OP_TI_CREATEDATUTC",
                table: "OP_TASKS_ITEMS");

            migrationBuilder.DropColumn(
                name: "OP_TI_REFERENCESTASKITEMID",
                table: "OP_TASKS_ITEMS");

            migrationBuilder.DropColumn(
                name: "OP_TI_REQUIREDQUANTITY",
                table: "OP_TASKS_ITEMS");
        }
    }
}
