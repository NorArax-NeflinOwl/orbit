using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Mobile.Data.Migrations
{
    /// <inheritdoc />
    public partial class OneOutboxForEveryEntityType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Outbox_Id",
                table: "Outbox");

            migrationBuilder.RenameColumn(
                name: "NoteServerId",
                table: "Outbox",
                newName: "ServerId");

            migrationBuilder.RenameColumn(
                name: "NoteLocalId",
                table: "Outbox",
                newName: "LocalId");

            migrationBuilder.AddColumn<string>(
                name: "EntityType",
                table: "Outbox",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Outbox_EntityType_Id",
                table: "Outbox",
                columns: new[] { "EntityType", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Outbox_EntityType_Id",
                table: "Outbox");

            migrationBuilder.DropColumn(
                name: "EntityType",
                table: "Outbox");

            migrationBuilder.RenameColumn(
                name: "ServerId",
                table: "Outbox",
                newName: "NoteServerId");

            migrationBuilder.RenameColumn(
                name: "LocalId",
                table: "Outbox",
                newName: "NoteLocalId");

            migrationBuilder.CreateIndex(
                name: "IX_Outbox_Id",
                table: "Outbox",
                column: "Id");
        }
    }
}
