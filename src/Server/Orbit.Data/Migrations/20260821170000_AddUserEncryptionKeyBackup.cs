using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserEncryptionKeyBackup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "WrappedPrivateKeyBase64",
                table: "Users",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrivateKeyWrapNonceBase64",
                table: "Users",
                type: "TEXT",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrivateKeySaltBase64",
                table: "Users",
                type: "TEXT",
                maxLength: 24,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PrivateKeyDerivationIterations",
                table: "Users",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WrappedPrivateKeyBase64",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PrivateKeyWrapNonceBase64",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PrivateKeySaltBase64",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PrivateKeyDerivationIterations",
                table: "Users");
        }
    }
}
