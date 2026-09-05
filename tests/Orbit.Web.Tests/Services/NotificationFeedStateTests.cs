using Orbit.Contracts.Notifications;
using Orbit.Web.Services;
using Xunit;

namespace Orbit.Web.Tests.Services;

/// <summary>
/// Which unread notifications a page settles by being opened. Reaching the page a notification is about
/// is the same as having read it, and a task list has two pages - so the deep editor has to settle a
/// reminder pointing at the list, or the badge stays lit over something being looked at.
/// </summary>
public sealed class NotificationFeedStateTests
{
    private static NotificationEntryDto At(string url)
        => new(Guid.NewGuid(), "TaskOverdue", "Overdue task", "Body", url, DateTimeOffset.UtcNow, IsRead: false);

    private static NotificationFeedState WithUnread(params string[] urls)
    {
        var state = new NotificationFeedState();
        state.Set([.. urls.Select(At)]);
        return state;
    }

    /// <summary>
    /// What lets a list say which of its rows the bell means - see Dashboard.razor, which asks this per
    /// row so a notification about one task list marks that list rather than only the card.
    /// </summary>
    [Fact]
    public void A_thing_with_a_notification_pointing_at_it_has_news()
        => Assert.True(WithUnread("/tasks/abc").HasNewsAbout("/tasks/abc"));

    /// <summary>An entry pointing deeper is still about the thing it is under - an entry on a list is that list's news.</summary>
    [Fact]
    public void News_about_something_inside_it_is_news_about_it()
        => Assert.True(WithUnread("/tasks/abc/items/xyz").HasNewsAbout("/tasks/abc"));

    [Fact]
    public void Another_things_notification_is_not_news_about_this_one()
    {
        var state = WithUnread("/tasks/abc");

        Assert.False(state.HasNewsAbout("/tasks/def"));
        // Matched at a path boundary, so a longer id starting with a shorter one is a different thing.
        Assert.False(state.HasNewsAbout("/tasks/ab"));
    }

    [Fact]
    public void Nothing_unread_is_news_about_nothing()
        => Assert.False(new NotificationFeedState().HasNewsAbout("/tasks/abc"));

    [Fact]
    public void The_page_a_notification_points_at_settles_it()
    {
        var state = WithUnread("/tasks/abc");

        Assert.Equal(["/tasks/abc"], state.UnreadUrlsSettledBy("/tasks/abc"));
    }

    [Fact]
    public void A_page_underneath_settles_it_too()
    {
        var state = WithUnread("/tasks/abc");

        // Opening the list's deep editor is reaching the list.
        Assert.Equal(["/tasks/abc"], state.UnreadUrlsSettledBy("/tasks/abc/edit"));
    }

    [Fact]
    public void Another_list_is_left_alone()
    {
        var state = WithUnread("/tasks/abc", "/tasks/abcdef");

        // Matched at a segment boundary, so a longer id that merely starts the same is not swept up.
        Assert.Equal(["/tasks/abc"], state.UnreadUrlsSettledBy("/tasks/abc/edit"));
    }

    [Fact]
    public void A_page_above_settles_nothing()
    {
        var state = WithUnread("/tasks/abc/edit");

        // The other way round is not true: being on the list is not being in its editor.
        Assert.Empty(state.UnreadUrlsSettledBy("/tasks/abc"));
    }
}
