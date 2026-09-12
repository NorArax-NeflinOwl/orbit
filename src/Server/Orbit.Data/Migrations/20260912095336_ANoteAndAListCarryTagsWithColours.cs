using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Data.Migrations
{
    /// <inheritdoc />
    public partial class ANoteAndAListCarryTagsWithColours : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OP_T_TAGSJSON",
                table: "OP_TASKS",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "OP_N_TAGSJSON",
                table: "OP_NOTES",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.CreateTable(
                name: "OS_TAGS_COLOURS",
                columns: table => new
                {
                    OS_TC_USERID = table.Column<Guid>(type: "uuid", nullable: false),
                    OS_TC_NORMALIZEDTAG = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OS_TC_TAG = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OS_TC_COLOUR = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    OS_TC_UPDATEDATUTC = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OS_TAGS_COLOURS", x => new { x.OS_TC_USERID, x.OS_TC_NORMALIZEDTAG });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OS_TAGS_COLOURS");

            migrationBuilder.DropColumn(
                name: "OP_T_TAGSJSON",
                table: "OP_TASKS");

            migrationBuilder.DropColumn(
                name: "OP_N_TAGSJSON",
                table: "OP_NOTES");
        }
    }
}
