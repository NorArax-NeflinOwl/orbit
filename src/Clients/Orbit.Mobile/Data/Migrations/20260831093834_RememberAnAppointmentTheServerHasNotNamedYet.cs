using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Mobile.Data.Migrations
{
    /// <inheritdoc />
    public partial class RememberAnAppointmentTheServerHasNotNamedYet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PendingCalendarLinks",
                columns: table => new
                {
                    TaskItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TaskListLocalId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CalendarEventLocalId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingCalendarLinks", x => x.TaskItemId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PendingCalendarLinks");
        }
    }
}
