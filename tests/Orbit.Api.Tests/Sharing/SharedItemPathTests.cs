using Orbit.Core.Notifications;
using Xunit;

namespace Orbit.Api.Tests.Sharing;

/// <summary>
/// The one mapping between a shareable kind and its name in an address. Five places read or write it -
/// the notifier, the offer endpoint, the invitation page, the client that asks for an offer, and the
/// conversation that settles a notification after accepting there - so it is worth pinning down that it
/// reads back what it writes, and that the names themselves do not move.
/// </summary>
public sealed class SharedItemPathTests
{
    [Theory]
    [InlineData(SharedItemKind.Note, "note")]
    [InlineData(SharedItemKind.TaskList, "tasklist")]
    [InlineData(SharedItemKind.CalendarEvent, "event")]
    [InlineData(SharedItemKind.Inventory, "inventory")]
    [InlineData(SharedItemKind.Location, "location")]
    public void Each_kind_has_the_name_it_has_always_had(SharedItemKind kind, string expected)
    {
        // These names are in notification rows already written and in paths already handed to a phone:
        // renaming one orphans every invitation that carried it.
        Assert.Equal(expected, SharedItemPath.For(kind));
        Assert.Equal(kind, SharedItemPath.KindOf(expected));
    }

    [Fact]
    public void Every_kind_survives_the_round_trip()
        => Assert.All(
            Enum.GetValues<SharedItemKind>(),
            kind => Assert.Equal(kind, SharedItemPath.KindOf(SharedItemPath.For(kind))));

    /// <summary>
    /// A name from a newer build reads as nothing rather than as something wrong - the endpoint answers
    /// "no such offer" for it, and the page says it is for something it does not know about.
    /// </summary>
    [Theory]
    [InlineData("something-new")]
    [InlineData("")]
    [InlineData(null)]
    public void A_name_this_build_does_not_know_is_no_kind_at_all(string? path)
        => Assert.Null(SharedItemPath.KindOf(path));

    /// <summary>Read back whatever case it arrives in: an address is not a place to be strict about that.</summary>
    [Fact]
    public void The_name_is_read_whatever_case_it_arrives_in()
        => Assert.Equal(SharedItemKind.TaskList, SharedItemPath.KindOf("TaskList"));
}
