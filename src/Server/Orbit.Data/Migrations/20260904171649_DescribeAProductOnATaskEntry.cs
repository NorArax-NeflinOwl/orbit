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

            migrationBuilder.CreateTable(
                name: "OP_TASKS_PRODUCT_CATEGORIES",
                columns: table => new
                {
                    OP_TPC_TASKITEMID = table.Column<Guid>(type: "uuid", nullable: false),
                    OP_TPC_CATEGORY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OP_TPC_POSITION = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OP_TASKS_PRODUCT_CATEGORIES", x => new { x.OP_TPC_TASKITEMID, x.OP_TPC_CATEGORY });
                    table.ForeignKey(
                        name: "FK_OP_TASKS_PRODUCT_CATEGORIES_OP_TASKS_ITEMS_OP_TPC_TASKITEMID",
                        column: x => x.OP_TPC_TASKITEMID,
                        principalTable: "OP_TASKS_ITEMS",
                        principalColumn: "OP_TI_ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OP_TASKS_PRODUCT_CATEGORIES_OP_TPC_CATEGORY",
                table: "OP_TASKS_PRODUCT_CATEGORIES",
                column: "OP_TPC_CATEGORY");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OP_TASKS_PRODUCT_CATEGORIES");

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
