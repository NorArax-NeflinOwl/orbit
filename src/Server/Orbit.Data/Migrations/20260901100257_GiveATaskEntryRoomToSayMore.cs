using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Data.Migrations
{
    /// <inheritdoc />
    public partial class GiveATaskEntryRoomToSayMore : Migration
    {
        /// <inheritdoc />
        // Widening, which loses nothing: every stored value already fits in 500 and so fits in 2000.
        // EF warns about possible data loss for any AlterColumn, and here the warning belongs to Down
        // rather than Up - going back would truncate anything written since, which is why nothing
        // should call it once this has shipped.
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "TaskItemEntity",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "TaskItemEntity",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000);
        }
    }
}
