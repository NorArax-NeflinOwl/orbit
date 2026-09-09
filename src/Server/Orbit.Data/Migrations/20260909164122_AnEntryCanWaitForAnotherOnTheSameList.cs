using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Data.Migrations
{
    /// <inheritdoc />
    public partial class AnEntryCanWaitForAnotherOnTheSameList : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OL_TASKS_STEPS",
                columns: table => new
                {
                    OL_TS_TASKITEMID = table.Column<Guid>(type: "uuid", nullable: false),
                    OL_TS_WAITSFORTASKITEMID = table.Column<Guid>(type: "uuid", nullable: false),
                    OL_TS_POSITION = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OL_TASKS_STEPS", x => new { x.OL_TS_TASKITEMID, x.OL_TS_WAITSFORTASKITEMID });
                    table.ForeignKey(
                        name: "FK_OL_TASKS_STEPS_OP_TASKS_ITEMS_OL_TS_TASKITEMID",
                        column: x => x.OL_TS_TASKITEMID,
                        principalTable: "OP_TASKS_ITEMS",
                        principalColumn: "OP_TI_ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OL_TASKS_STEPS_OL_TS_WAITSFORTASKITEMID",
                table: "OL_TASKS_STEPS",
                column: "OL_TS_WAITSFORTASKITEMID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OL_TASKS_STEPS");
        }
    }
}
