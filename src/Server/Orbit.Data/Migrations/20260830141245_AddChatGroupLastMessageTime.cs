using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddChatGroupLastMessageTime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastMessageAtUtc",
                table: "ChatGroups",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            // A group that existed before this column did has never stamped it, and the default would
            // sort every one of them below everything else forever. The day it was made is the honest
            // answer to "when did something last happen here" for a group nobody has written in - which
            // is also what ChatGroup.Create gives a new one.
            migrationBuilder.Sql("UPDATE \"ChatGroups\" SET \"LastMessageAtUtc\" = \"CreatedAtUtc\";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastMessageAtUtc",
                table: "ChatGroups");
        }
    }
}
