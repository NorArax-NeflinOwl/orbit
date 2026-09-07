using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Data.Migrations
{
    /// <inheritdoc />
    public partial class FileNotesAndListsInFolders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OP_T_FOLDERID",
                table: "OP_TASKS",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OP_N_FOLDERID",
                table: "OP_NOTES",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OP_FOLDERS",
                columns: table => new
                {
                    OP_F_ID = table.Column<Guid>(type: "uuid", nullable: false),
                    OP_F_USERID = table.Column<Guid>(type: "uuid", nullable: false),
                    OP_F_NAME = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OP_F_CREATEDATUTC = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    OP_F_UPDATEDATUTC = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OP_FOLDERS", x => x.OP_F_ID);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OP_FOLDERS_OP_F_USERID",
                table: "OP_FOLDERS",
                column: "OP_F_USERID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OP_FOLDERS");

            migrationBuilder.DropColumn(
                name: "OP_T_FOLDERID",
                table: "OP_TASKS");

            migrationBuilder.DropColumn(
                name: "OP_N_FOLDERID",
                table: "OP_NOTES");
        }
    }
}
