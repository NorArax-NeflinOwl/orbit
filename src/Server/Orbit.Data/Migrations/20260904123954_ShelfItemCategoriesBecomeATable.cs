using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Data.Migrations
{
    /// <summary>
    /// A shelf item's category becomes as many as apply, the way a task entry's already was.
    ///
    /// Written by hand rather than left as scaffolded: the generated version dropped the column first
    /// and created the table afterwards, which is every category on every shelf in the deployment.
    /// The order here is create, copy, drop - and the copy is the whole point of the migration.
    /// </summary>
    public partial class ShelfItemCategoriesBecomeATable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // OP_INVENTORIES_ITEMS has had no primary key since the rename to the Orbit convention:
            // that migration dropped PK_InventoryItems along with every other table's, and added
            // twenty-nine of the thirty back. Nothing noticed, because nothing had pointed at the table
            // until now - a foreign key needs a unique constraint to reference, and this is the first
            // one. The model has always declared it (see OrbitDbContext's HasKey), so this is the
            // database catching up with what EF already believed.
            //
            // Guarded rather than added outright: a database restored from before the rename still has
            // it, and this migration must not be the thing that fails there.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM pg_constraint
                        WHERE conrelid = '"OP_INVENTORIES_ITEMS"'::regclass AND contype = 'p')
                    THEN
                        ALTER TABLE "OP_INVENTORIES_ITEMS"
                            ADD CONSTRAINT "PK_OP_INVENTORIES_ITEMS" PRIMARY KEY ("OP_II_ID");
                    END IF;
                END $$;
                """);

            migrationBuilder.CreateTable(
                name: "OP_INVENTORIES_CATEGORIES",
                columns: table => new
                {
                    OP_IC_INVENTORYITEMID = table.Column<Guid>(type: "uuid", nullable: false),
                    OP_IC_CATEGORY = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OP_IC_POSITION = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OP_INVENTORIES_CATEGORIES", x => new { x.OP_IC_INVENTORYITEMID, x.OP_IC_CATEGORY });
                    table.ForeignKey(
                        name: "FK_OP_INVENTORIES_CATEGORIES_OP_INVENTORIES_ITEMS_OP_IC_INVENT~",
                        column: x => x.OP_IC_INVENTORYITEMID,
                        principalTable: "OP_INVENTORIES_ITEMS",
                        principalColumn: "OP_II_ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OP_INVENTORIES_CATEGORIES_OP_IC_CATEGORY",
                table: "OP_INVENTORIES_CATEGORIES",
                column: "OP_IC_CATEGORY");

            // Everything already filed becomes that item's first - and only - category. Items filed
            // under nothing are skipped rather than given an empty row, which the primary key would
            // accept and the domain would immediately tidy away.
            migrationBuilder.Sql("""
                INSERT INTO "OP_INVENTORIES_CATEGORIES" ("OP_IC_INVENTORYITEMID", "OP_IC_CATEGORY", "OP_IC_POSITION")
                SELECT "OP_II_ID", TRIM("OP_II_CATEGORY"), 0
                FROM "OP_INVENTORIES_ITEMS"
                WHERE TRIM(COALESCE("OP_II_CATEGORY", '')) <> '';
                """);

            migrationBuilder.DropColumn(
                name: "OP_II_CATEGORY",
                table: "OP_INVENTORIES_ITEMS");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OP_II_CATEGORY",
                table: "OP_INVENTORIES_ITEMS",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            // The first of them back into the column, which is all a single field can hold. Going back
            // therefore loses whatever was filed second and after - said here rather than discovered,
            // since that is exactly what this migration existed to make possible.
            migrationBuilder.Sql("""
                UPDATE "OP_INVENTORIES_ITEMS" AS items
                SET "OP_II_CATEGORY" = COALESCE((
                    SELECT categories."OP_IC_CATEGORY"
                    FROM "OP_INVENTORIES_CATEGORIES" AS categories
                    WHERE categories."OP_IC_INVENTORYITEMID" = items."OP_II_ID"
                    ORDER BY categories."OP_IC_POSITION"
                    LIMIT 1), '');
                """);

            migrationBuilder.DropTable(
                name: "OP_INVENTORIES_CATEGORIES");

            // The primary key added above is deliberately left in place. It was missing by accident
            // rather than by design, and taking it away again on the way down would put the database
            // back into a state the model never described.
        }
    }
}
