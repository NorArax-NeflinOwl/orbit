using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Data.Migrations
{
    /// <inheritdoc />
    public partial class HandAPlaceToSomebody : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OP_PLACES_SHARED",
                columns: table => new
                {
                    OP_PLS_ID = table.Column<Guid>(type: "uuid", nullable: false),
                    OP_PLS_SOURCEPLACEID = table.Column<Guid>(type: "uuid", nullable: false),
                    OP_PLS_OWNERUSERID = table.Column<Guid>(type: "uuid", nullable: false),
                    OP_PLS_RECIPIENTUSERID = table.Column<Guid>(type: "uuid", nullable: false),
                    OP_PLS_ACCESSLEVEL = table.Column<string>(type: "text", nullable: false),
                    OP_PLS_CREATEDATUTC = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    OP_PLS_ACCEPTEDATUTC = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OP_PLACES_SHARED", x => x.OP_PLS_ID);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OP_PLACES_SHARED_OP_PLS_SOURCEPLACEID_OP_PLS_RECIPIENTUSERID",
                table: "OP_PLACES_SHARED",
                columns: new[] { "OP_PLS_SOURCEPLACEID", "OP_PLS_RECIPIENTUSERID" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OP_PLACES_SHARED");
        }
    }
}
