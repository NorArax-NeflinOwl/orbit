using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Data.Migrations
{
    /// <inheritdoc />
    public partial class DescribeAProductOnATaskEntry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OP_TI_PRODUCTCATEGORY",
                table: "OP_TASKS_ITEMS",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "OP_TI_PRODUCTEXPIRYDATE",
                table: "OP_TASKS_ITEMS",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OP_TI_PRODUCTEXPIRYNOTIFICATIONCHANNEL",
                table: "OP_TASKS_ITEMS",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "OP_TI_PRODUCTISCHECKEDREGULARLY",
                table: "OP_TASKS_ITEMS",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OP_TI_PRODUCTMINIMUMQUANTITY",
                table: "OP_TASKS_ITEMS",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OP_TI_PRODUCTQUANTITY",
                table: "OP_TASKS_ITEMS",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OP_TI_PRODUCTTYPE",
                table: "OP_TASKS_ITEMS",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OP_TI_PRODUCTUNIT",
                table: "OP_TASKS_ITEMS",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OP_TI_PRODUCTCATEGORY",
                table: "OP_TASKS_ITEMS");

            migrationBuilder.DropColumn(
                name: "OP_TI_PRODUCTEXPIRYDATE",
                table: "OP_TASKS_ITEMS");

            migrationBuilder.DropColumn(
                name: "OP_TI_PRODUCTEXPIRYNOTIFICATIONCHANNEL",
                table: "OP_TASKS_ITEMS");

            migrationBuilder.DropColumn(
                name: "OP_TI_PRODUCTISCHECKEDREGULARLY",
                table: "OP_TASKS_ITEMS");

            migrationBuilder.DropColumn(
                name: "OP_TI_PRODUCTMINIMUMQUANTITY",
                table: "OP_TASKS_ITEMS");

            migrationBuilder.DropColumn(
                name: "OP_TI_PRODUCTQUANTITY",
                table: "OP_TASKS_ITEMS");

            migrationBuilder.DropColumn(
                name: "OP_TI_PRODUCTTYPE",
                table: "OP_TASKS_ITEMS");

            migrationBuilder.DropColumn(
                name: "OP_TI_PRODUCTUNIT",
                table: "OP_TASKS_ITEMS");
        }
    }
}
