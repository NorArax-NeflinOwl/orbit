using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Data.Migrations
{
    /// <inheritdoc />
    public partial class SealAPlaceUnlessItsOwnerSaysOtherwise : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "OP_P_NAME",
                table: "OP_PLACES",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AddColumn<string>(
                name: "OP_P_ENCRYPTEDCIPHERTEXT",
                table: "OP_PLACES",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OP_P_ENCRYPTEDNONCE",
                table: "OP_PLACES",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "OP_P_ISPRIVATE",
                table: "OP_PLACES",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OP_P_ENCRYPTEDCIPHERTEXT",
                table: "OP_PLACES");

            migrationBuilder.DropColumn(
                name: "OP_P_ENCRYPTEDNONCE",
                table: "OP_PLACES");

            migrationBuilder.DropColumn(
                name: "OP_P_ISPRIVATE",
                table: "OP_PLACES");

            migrationBuilder.AlterColumn<string>(
                name: "OP_P_NAME",
                table: "OP_PLACES",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldDefaultValue: "");
        }
    }
}
