using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWarehouseEditLock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LockExpiresAtUtc",
                table: "Warehouses",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LockedByUserId",
                table: "Warehouses",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LockedByUserName",
                table: "Warehouses",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LockExpiresAtUtc",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "LockedByUserId",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "LockedByUserName",
                table: "Warehouses");
        }
    }
}
