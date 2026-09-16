using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Data.Migrations
{
    /// <inheritdoc />
    public partial class ANoteKeepsItsPictures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The rows, not the bytes: what a note holds and how much of its 50 MB that is. The bytes
            // are the picture store's - see Orbit.Api.Notes.Pictures.
            migrationBuilder.CreateTable(
                name: "OP_NOTES_PICTURES",
                columns: table => new
                {
                    OP_NP_ID = table.Column<Guid>(type: "uuid", nullable: false),
                    OP_NP_NOTEID = table.Column<Guid>(type: "uuid", nullable: false),
                    OP_NP_OWNERUSERID = table.Column<Guid>(type: "uuid", nullable: false),
                    OP_NP_SIZEBYTES = table.Column<long>(type: "bigint", nullable: false),
                    OP_NP_CONTENTTYPE = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    OP_NP_ISSEALED = table.Column<bool>(type: "boolean", nullable: false),
                    OP_NP_CREATEDATUTC = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OP_NOTES_PICTURES", x => x.OP_NP_ID);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OP_NOTES_PICTURES_OP_NP_NOTEID",
                table: "OP_NOTES_PICTURES",
                column: "OP_NP_NOTEID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OP_NOTES_PICTURES");
        }
    }
}
