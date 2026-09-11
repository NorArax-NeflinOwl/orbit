using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Data.Migrations
{
    /// <inheritdoc />
    public partial class LetAnEntryBeDoneAnyOneOfSeveralWays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OP_TASKS_ALTERNATIVES",
                columns: table => new
                {
                    OP_TA_TASKITEMID = table.Column<Guid>(type: "uuid", nullable: false),
                    OP_TA_POSITION = table.Column<int>(type: "integer", nullable: false),
                    OP_TA_DESCRIPTION = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    OP_TA_LINKEDTASKLISTID = table.Column<Guid>(type: "uuid", nullable: true),
                    OP_TA_ISDONE = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OP_TASKS_ALTERNATIVES", x => new { x.OP_TA_TASKITEMID, x.OP_TA_POSITION });
                    table.ForeignKey(
                        name: "FK_OP_TASKS_ALTERNATIVES_OP_TASKS_ITEMS_OP_TA_TASKITEMID",
                        column: x => x.OP_TA_TASKITEMID,
                        principalTable: "OP_TASKS_ITEMS",
                        principalColumn: "OP_TI_ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OP_TASKS_ALTERNATIVES_OP_TA_LINKEDTASKLISTID",
                table: "OP_TASKS_ALTERNATIVES",
                column: "OP_TA_LINKEDTASKLISTID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OP_TASKS_ALTERNATIVES");
        }
    }
}
