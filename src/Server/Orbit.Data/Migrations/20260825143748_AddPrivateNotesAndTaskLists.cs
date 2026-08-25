using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPrivateNotesAndTaskLists : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EncryptedCiphertext",
                table: "Tasks",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EncryptedNonce",
                table: "Tasks",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPrivate",
                table: "Tasks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "EncryptedCiphertext",
                table: "Notes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EncryptedNonce",
                table: "Notes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPrivate",
                table: "Notes",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EncryptedCiphertext",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "EncryptedNonce",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "IsPrivate",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "EncryptedCiphertext",
                table: "Notes");

            migrationBuilder.DropColumn(
                name: "EncryptedNonce",
                table: "Notes");

            migrationBuilder.DropColumn(
                name: "IsPrivate",
                table: "Notes");
        }
    }
}
