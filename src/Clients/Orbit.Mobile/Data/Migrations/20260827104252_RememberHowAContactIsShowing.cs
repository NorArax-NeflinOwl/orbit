using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Mobile.Data.Migrations
{
    /// <inheritdoc />
    public partial class RememberHowAContactIsShowing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PresenceStatus",
                table: "Contacts",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PresenceStatus",
                table: "Contacts");
        }
    }
}
