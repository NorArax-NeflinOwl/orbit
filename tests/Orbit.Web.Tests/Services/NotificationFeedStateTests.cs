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
