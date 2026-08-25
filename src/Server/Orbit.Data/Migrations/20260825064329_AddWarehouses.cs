using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWarehouses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "InventoryManagedTaskLists",
                newName: "WarehouseId");

            migrationBuilder.RenameIndex(
                name: "IX_InventoryManagedTaskLists_UserId",
                table: "InventoryManagedTaskLists",
                newName: "IX_InventoryManagedTaskLists_WarehouseId");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "InventoryItems",
                newName: "WarehouseId");

            migrationBuilder.RenameIndex(
                name: "IX_InventoryItems_UserId",
                table: "InventoryItems",
                newName: "IX_InventoryItems_WarehouseId");

            migrationBuilder.CreateTable(
                name: "Warehouses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Warehouses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WarehouseShares",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceWarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccessLevel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AcceptedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WarehouseShares", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Warehouses_UserId",
                table: "Warehouses",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseShares_RecipientUserId",
                table: "WarehouseShares",
                column: "RecipientUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseShares_SourceWarehouseId_RecipientUserId",
                table: "WarehouseShares",
                columns: new[] { "SourceWarehouseId", "RecipientUserId" });

            // The renames above only changed what the column is *called* - every existing row still holds
            // the owner's user id in what is now a warehouse id. Give each user who already had inventory
            // data one warehouse and repoint their rows at it, otherwise those rows would silently point
            // at warehouses that never existed and their items would vanish from the app.
            migrationBuilder.Sql("""
                INSERT INTO "Warehouses" ("Id", "UserId", "Name", "CreatedAtUtc", "UpdatedAtUtc")
                SELECT gen_random_uuid(), owner."UserId", 'My warehouse', now(), now()
                FROM (
                    SELECT DISTINCT "WarehouseId" AS "UserId" FROM "InventoryItems"
                    UNION
                    SELECT DISTINCT "WarehouseId" AS "UserId" FROM "InventoryManagedTaskLists"
                ) AS owner;
                """);

            migrationBuilder.Sql("""
                UPDATE "InventoryItems" AS items
                SET "WarehouseId" = warehouses."Id"
                FROM "Warehouses" AS warehouses
                WHERE warehouses."UserId" = items."WarehouseId";
                """);

            migrationBuilder.Sql("""
                UPDATE "InventoryManagedTaskLists" AS managed
                SET "WarehouseId" = warehouses."Id"
                FROM "Warehouses" AS warehouses
                WHERE warehouses."UserId" = managed."WarehouseId";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Puts the owner's user id back where the warehouse id currently sits, before the table that
            // maps one to the other goes away. Necessarily lossy the other way round: a user who created
            // several warehouses after this migration ran ends up with all their items collapsed back
            // under their own id, since the pre-warehouse schema had nowhere to record the distinction.
            migrationBuilder.Sql("""
                UPDATE "InventoryItems" AS items
                SET "WarehouseId" = warehouses."UserId"
                FROM "Warehouses" AS warehouses
                WHERE warehouses."Id" = items."WarehouseId";
                """);

            migrationBuilder.Sql("""
                UPDATE "InventoryManagedTaskLists" AS managed
                SET "WarehouseId" = warehouses."UserId"
                FROM "Warehouses" AS warehouses
                WHERE warehouses."Id" = managed."WarehouseId";
                """);

            // Collapsing several warehouses back onto one user can leave duplicate rows behind, which the
            // restored unique index on UserId would reject - keep the oldest row per user, matching the
            // one-managed-list-per-user rule the old schema enforced.
            migrationBuilder.Sql("""
                DELETE FROM "InventoryManagedTaskLists"
                WHERE "Id" NOT IN (
                    SELECT MIN("Id"::text)::uuid FROM "InventoryManagedTaskLists" GROUP BY "WarehouseId"
                );
                """);

            migrationBuilder.DropTable(
                name: "Warehouses");

            migrationBuilder.DropTable(
                name: "WarehouseShares");

            migrationBuilder.RenameColumn(
                name: "WarehouseId",
                table: "InventoryManagedTaskLists",
                newName: "UserId");

            migrationBuilder.RenameIndex(
                name: "IX_InventoryManagedTaskLists_WarehouseId",
                table: "InventoryManagedTaskLists",
                newName: "IX_InventoryManagedTaskLists_UserId");

            migrationBuilder.RenameColumn(
                name: "WarehouseId",
                table: "InventoryItems",
                newName: "UserId");

            migrationBuilder.RenameIndex(
                name: "IX_InventoryItems_WarehouseId",
                table: "InventoryItems",
                newName: "IX_InventoryItems_UserId");
        }
    }
}
