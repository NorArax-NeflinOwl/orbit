using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMobilePushTransport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "P256dhBase64",
                table: "PushSubscriptions",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Endpoint",
                table: "PushSubscriptions",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "AuthBase64",
                table: "PushSubscriptions",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "DevicePlatform",
                table: "PushSubscriptions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeviceToken",
                table: "PushSubscriptions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Transport",
                table: "PushSubscriptions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            // Every row that existed before this migration is a browser subscription - the only kind
            // there was. Without this they would carry an empty transport and rely on the repository's
            // fallback rather than saying what they are.
            migrationBuilder.Sql(
                @"UPDATE ""PushSubscriptions"" SET ""Transport"" = 'WebPush' WHERE ""Transport"" = '';");

            migrationBuilder.CreateIndex(
                name: "IX_PushSubscriptions_DeviceToken",
                table: "PushSubscriptions",
                column: "DeviceToken",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PushSubscriptions_DeviceToken",
                table: "PushSubscriptions");

            migrationBuilder.DropColumn(
                name: "DevicePlatform",
                table: "PushSubscriptions");

            migrationBuilder.DropColumn(
                name: "DeviceToken",
                table: "PushSubscriptions");

            migrationBuilder.DropColumn(
                name: "Transport",
                table: "PushSubscriptions");

            migrationBuilder.AlterColumn<string>(
                name: "P256dhBase64",
                table: "PushSubscriptions",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Endpoint",
                table: "PushSubscriptions",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AuthBase64",
                table: "PushSubscriptions",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
