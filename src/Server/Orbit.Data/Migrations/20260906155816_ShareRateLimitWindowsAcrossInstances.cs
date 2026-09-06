using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Data.Migrations
{
    /// <inheritdoc />
    public partial class ShareRateLimitWindowsAcrossInstances : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OS_RATE_LIMITS",
                columns: table => new
                {
                    OS_RL_PARTITION = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    OS_RL_WINDOWSTART = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    OS_RL_COUNT = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OS_RATE_LIMITS", x => new { x.OS_RL_PARTITION, x.OS_RL_WINDOWSTART });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OS_RATE_LIMITS");
        }
    }
}
