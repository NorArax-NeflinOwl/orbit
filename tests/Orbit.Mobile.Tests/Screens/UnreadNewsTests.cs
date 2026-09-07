using Orbit.Contracts.Notifications;
using Orbit.Mobile.Screens;
using Xunit;

namespace Orbit.Mobile.Tests.Screens;

/// <summary>
/// Which unread notification is about which thing. The whole of it is one matching rule, and getting
/// that rule wrong is invisible: a mark that should be there is simply absent, and one that should not
/// be is on a row nobody looks at twice. So it is written down here rather than left to the screens.
/// </summary>
public sealed class UnreadNewsTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-08-27T09:00:00Z");

    [Fact]
    public void News_pointing_at_exactly_this_thing_is_about_it()
    {
        var unread = Addresses("/tasks/1111");

        Assert.True(UnreadNews.About(unread, "/tasks/1111"));
    }

    /// <summary>
    /// A notification about a page under this one settles it too: looking at a task list's innards is
    /// reaching that task list, and a reminder about a list you are reading is one you have read.
    /// </summary>
    [Fact]
    public void News_pointing_deeper_into_this_thing_is_about_it()
    {
        var unread = Addresses("/tasks/1111/edit");

        Assert.True(UnreadNews.About(unread, "/tasks/1111"));
    }

    [Fact]
    public void News_about_something_else_in_the_same_section_is_not_about_it()
    {
        var unread = Addresses("/tasks/2222");

        Assert.False(UnreadNews.About(unread, "/tasks/1111"));
    }

    /// <summary>
    /// The one the plain prefix test this replaces got wrong. Matched at a path-segment boundary, so a
    /// thing whose address merely starts the same way is a different thing.
    /// </summary>
    [Fact]
    public void News_about_a_thing_whose_address_merely_starts_the_same_way_is_not_about_it()
    {
        var unread = Addresses("/tasks/1111bc");

        Assert.False(UnreadNews.About(unread, "/tasks/1111"));
    }

    [Fact]
    public void The_section_itself_is_matched_by_news_about_anything_in_it()
    {
        var unread = Addresses("/chat/groups/3333");

        Assert.True(UnreadNews.About(unread, "/chat/groups"));
        Assert.False(UnreadNews.About(unread, "/chat/3333"));
    }

    /// <summary>Case is not what tells two addresses apart - a guid is written either way.</summary>
    [Fact]
    public void Case_makes_no_difference()
    {
        var unread = Addresses("/TASKS/AbCd");

        Assert.True(UnreadNews.About(unread, "/tasks/abcd"));
    }

    /// <summary>
    /// A notification with nowhere to go - one that is only words - is dropped rather than kept as an
    /// empty address, which every question would otherwise match.
    /// </summary>
    [Fact]
    public void An_entry_pointing_nowhere_is_not_an_address()
    {
        var addresses = UnreadNews.AddressesIn([Entry(null), Entry(string.Empty), Entry("/map")]);

        Assert.Equal(["/map"], addresses);
    }

    [Fact]
    public void Nothing_unread_means_nothing_is_about_anything()
        => Assert.False(UnreadNews.About([], "/tasks/1111"));

    private static IReadOnlyList<string> Addresses(params string[] urls)
        => UnreadNews.AddressesIn(urls.Select(Entry));

    private static NotificationEntryDto Entry(string? url)
        => new(Guid.NewGuid(), "Test", "Something happened", "About it", url, Now, IsRead: false);
}
