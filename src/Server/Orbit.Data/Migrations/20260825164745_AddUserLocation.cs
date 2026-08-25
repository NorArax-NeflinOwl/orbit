using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LocationAddress",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "LocationLatitude",
                table: "Users",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "LocationLongitude",
                table: "Users",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LocationRecordedAtUtc",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LocationAddress",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LocationLatitude",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LocationLongitude",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LocationRecordedAtUtc",
                table: "Users");
        }
    }
}
