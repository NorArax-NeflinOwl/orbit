using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Data.Migrations
{
    /// <inheritdoc />
    public partial class TasksCardFiltersMadeOfTags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OS_TASKS_TAG_FILTERS",
                columns: table => new
                {
                    OS_TTF_ID = table.Column<Guid>(type: "uuid", nullable: false),
                    OS_TTF_USERID = table.Column<Guid>(type: "uuid", nullable: false),
                    OS_TTF_TAGSJSON = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    OS_TTF_MATCHESALL = table.Column<bool>(type: "boolean", nullable: false),
                    OS_TTF_CREATEDATUTC = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OS_TASKS_TAG_FILTERS", x => x.OS_TTF_ID);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OS_TASKS_TAG_FILTERS_OS_TTF_USERID",
                table: "OS_TASKS_TAG_FILTERS",
                column: "OS_TTF_USERID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OS_TASKS_TAG_FILTERS");
        }
    }
}
