using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Mobile.Data.Migrations
{
    /// <inheritdoc />
    public partial class SayHowToReachSomebody : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Contacts",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Email",
                table: "Contacts");
        }
    }
}
