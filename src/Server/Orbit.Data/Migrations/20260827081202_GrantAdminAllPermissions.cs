using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Data.Migrations
{
    /// <summary>
    /// Gives the "admin" account every permission, so the deployment that introduces the gate is not one
    /// where nobody can reach anything. Data rather than schema, deliberately: the previous migration
    /// creates a table that starts empty, and an empty table means every account - including the one
    /// that would hand out the codes - loses chat, location and sharing at once.
    ///
    /// Matched by username, case-insensitively, and a no-op where no such account exists: this creates
    /// nobody. Re-running it grants nothing twice.
    /// </summary>
    /// <inheritdoc />
    public partial class GrantAdminAllPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT INTO "UserPermissions" ("UserId", "Permission", "GrantedAtUtc")
                SELECT "Users"."Id", granted."Permission", NOW()
                FROM "Users"
                CROSS JOIN (VALUES ('Location'), ('Chat'), ('GroupChat'), ('Sharing')) AS granted("Permission")
                WHERE LOWER("Users"."UserName") = 'admin'
                ON CONFLICT ("UserId", "Permission") DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM "UserPermissions"
                WHERE "UserId" IN (SELECT "Id" FROM "Users" WHERE LOWER("UserName") = 'admin');
                """);
        }
    }
}
