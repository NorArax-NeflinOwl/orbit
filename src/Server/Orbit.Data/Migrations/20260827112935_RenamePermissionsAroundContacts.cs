using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Data.Migrations
{
    /// <summary>
    /// Renames what is already stored to match the permissions as they are now: what used to be "Chat"
    /// is "Contacts" - being able to see other people at all - and what used to be "GroupChat" is
    /// "Chat", covering every conversation rather than only the group ones.
    ///
    /// Done through a name nothing else uses, because the two renames pass through each other. An
    /// account holding both is the ordinary case, and (user, 'Chat') has to stop existing before
    /// (user, 'GroupChat') can become it - the pair is the primary key, and a row-by-row update that
    /// happened to reach GroupChat first would collide with the Chat row still sitting there.
    /// </summary>
    /// <inheritdoc />
    public partial class RenamePermissionsAroundContacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "UserPermissions" SET "Permission" = 'PendingContacts' WHERE "Permission" = 'Chat';
                UPDATE "UserPermissions" SET "Permission" = 'Chat' WHERE "Permission" = 'GroupChat';
                UPDATE "UserPermissions" SET "Permission" = 'Contacts' WHERE "Permission" = 'PendingContacts';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "UserPermissions" SET "Permission" = 'PendingGroupChat' WHERE "Permission" = 'Chat';
                UPDATE "UserPermissions" SET "Permission" = 'Chat' WHERE "Permission" = 'Contacts';
                UPDATE "UserPermissions" SET "Permission" = 'GroupChat' WHERE "Permission" = 'PendingGroupChat';
                """);
        }
    }
}
