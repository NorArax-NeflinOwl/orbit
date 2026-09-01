using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orbit.Data.Migrations
{
    /// <summary>
    /// Where one reader's view of a conversation starts, once they have emptied it - see
    /// Orbit.Core.Chat.Contact.HistoryClearedAtUtc. Null for everybody who never has, which is what an
    /// existing row means and what it should mean: from the beginning.
    ///
    /// A line rather than deleting messages, because a one-to-one message is one row that both people
    /// read - deleting it would take words out of somebody else's conversation.
    /// </summary>
    public partial class SayWhereAReadersHistoryBegins : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "HistoryClearedAtUtc",
                table: "Contacts",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HistoryClearedAtUtc",
                table: "Contacts");
        }
    }
}
