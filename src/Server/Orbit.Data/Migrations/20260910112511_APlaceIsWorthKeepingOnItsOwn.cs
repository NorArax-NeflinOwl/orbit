using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Data.Migrations
{
    /// <inheritdoc />
    public partial class APlaceIsWorthKeepingOnItsOwn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OP_PLACES",
                columns: table => new
                {
                    OP_P_ID = table.Column<Guid>(type: "uuid", nullable: false),
                    OP_P_USERID = table.Column<Guid>(type: "uuid", nullable: false),
                    OP_P_NAME = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OP_P_DESCRIPTION = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false, defaultValue: ""),
                    OP_P_ADDRESS = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false, defaultValue: ""),
                    OP_P_LATITUDE = table.Column<double>(type: "double precision", nullable: false),
                    OP_P_LONGITUDE = table.Column<double>(type: "double precision", nullable: false),
                    OP_P_COLOUR = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: ""),
                    OP_P_PRIORITY = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Normal"),
                    OP_P_CREATEDATUTC = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    OP_P_UPDATEDATUTC = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OP_PLACES", x => x.OP_P_ID);
                });

            migrationBuilder.CreateTable(
                name: "OL_PLACES_TASKS",
                columns: table => new
                {
                    OL_PT_PLACEID = table.Column<Guid>(type: "uuid", nullable: false),
                    OL_PT_TASKLISTID = table.Column<Guid>(type: "uuid", nullable: false),
                    OL_PT_POSITION = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OL_PLACES_TASKS", x => new { x.OL_PT_PLACEID, x.OL_PT_TASKLISTID });
                    table.ForeignKey(
                        name: "FK_OL_PLACES_TASKS_OP_PLACES_OL_PT_PLACEID",
                        column: x => x.OL_PT_PLACEID,
                        principalTable: "OP_PLACES",
                        principalColumn: "OP_P_ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OP_PLACES_OP_P_USERID_OP_P_UPDATEDATUTC",
                table: "OP_PLACES",
                columns: new[] { "OP_P_USERID", "OP_P_UPDATEDATUTC" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OL_PLACES_TASKS");

            migrationBuilder.DropTable(
                name: "OP_PLACES");
        }
    }
}
