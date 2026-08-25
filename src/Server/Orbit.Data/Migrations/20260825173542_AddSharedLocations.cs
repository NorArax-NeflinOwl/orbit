using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSharedLocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SharedLocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SharerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CiphertextBase64 = table.Column<string>(type: "text", nullable: false),
                    NonceBase64 = table.Column<string>(type: "text", nullable: false),
                    IsContinuous = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SharedLocations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SharedLocations_RecipientUserId",
                table: "SharedLocations",
                column: "RecipientUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SharedLocations_SharerUserId_RecipientUserId",
                table: "SharedLocations",
                columns: new[] { "SharerUserId", "RecipientUserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SharedLocations");
        }
    }
}
