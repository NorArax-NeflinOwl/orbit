using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Mobile.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWarehousesToTheLocalStore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Warehouses",
                columns: table => new
                {
                    LocalId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ServerId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Items = table.Column<string>(type: "TEXT", nullable: false),
                    IsPrivate = table.Column<bool>(type: "INTEGER", nullable: false),
                    EncryptedCiphertext = table.Column<string>(type: "TEXT", nullable: true),
                    EncryptedNonce = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAtUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    IsShared = table.Column<bool>(type: "INTEGER", nullable: false),
                    SharedByUserName = table.Column<string>(type: "TEXT", nullable: true),
                    IsSharedWithOthers = table.Column<bool>(type: "INTEGER", nullable: false),
                    AccessLevel = table.Column<string>(type: "TEXT", nullable: false),
                    LastSyncedAtUtc = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Warehouses", x => x.LocalId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Warehouses_ServerId",
                table: "Warehouses",
                column: "ServerId",
                unique: true,
                filter: "\"ServerId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Warehouses");
        }
    }
}
