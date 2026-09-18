using Orbit.Mobile.Notifications;
using Xunit;

namespace Orbit.Mobile.Tests.Notifications;

/// <summary>
/// The paths here are not invented for the test - each is what a PushContent type in Orbit.Core
/// actually produces (ChatMessagePushContent, EventReminderPushContent, InventoryExpiryPushContent and
/// the rest). That makes this the place a server-side change to a notification's destination shows up
/// as a failure, rather than as a tap that silently goes nowhere on a phone.
/// </summary>
public sealed class NotificationRoutingTests
{
    [Fact]
    public void A_message_notification_leads_to_the_conversation_with_the_sender()
    {
        var senderUserId = Guid.NewGuid();

        var destination = NotificationDestination.Parse($"/chat/{senderUserId}");

        Assert.Equal(new NotificationDestination(NotificationTarget.Conversation, senderUserId), destination);
    }

    [Fact]
    public void A_group_invitation_leads_to_the_group_and_not_to_a_conversation()
    {
        // /chat/groups/{id} and /chat/{id} differ by one segment, and reading the group id as a user id
        // would open a conversation with nobody.
        var groupId = Guid.NewGuid();

        var destination = NotificationDestination.Parse($"/chat/groups/{groupId}");

        Assert.Equal(new NotificationDestination(NotificationTarget.GroupConversation, groupId), destination);
    }

    [Fact]
    public void A_task_reminder_leads_to_the_list_it_is_about()
    {
        var taskListId = Guid.NewGuid();

        var destination = NotificationDestination.Parse($"/tasks/{taskListId}");

        Assert.Equal(new NotificationDestination(NotificationTarget.TaskList, taskListId), destination);
    }

    [Fact]
    public void An_event_reminder_leads_to_the_calendar_and_drops_the_event_id()
    {
        // There is no screen for one event on its own, so carrying the id would hand it to something
        // that cannot use it. The calendar is the honest landing place.
        var destination = NotificationDestination.Parse($"/calendar/{Guid.NewGuid()}");

        Assert.Equal(new NotificationDestination(NotificationTarget.Calendar), destination);
    }

    [Theory]
    [InlineData("/inventory", NotificationTarget.Inventory)]
    [InlineData("/map", NotificationTarget.Map)]
    public void The_destinations_that_name_nothing_in_particular_still_lead_somewhere(string url, NotificationTarget expected)
    {
        var destination = NotificationDestination.Parse(url);

        Assert.Equal(new NotificationDestination(expected), destination);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("/somewhere-invented-later")]
    [InlineData("/chat/not-a-guid")]
    [InlineData("/tasks/12345")]
    public void A_path_this_build_does_not_know_is_not_a_destination(string? url)
    {
        // An older app against a newer server. The entry must still list and still read - refusing to
        // parse is how the feed knows not to offer a tap, and is much better than throwing.
        Assert.Null(NotificationDestination.Parse(url));
    }

    [Fact]
    public void A_trailing_slash_does_not_change_where_it_leads()
    {
        var groupId = Guid.NewGuid();

        Assert.Equal(
            NotificationDestination.Parse($"/chat/groups/{groupId}"),
            NotificationDestination.Parse($"/chat/groups/{groupId}/"));
    }

    /// <summary>
    /// Nor does what the browser is asked to do on arrival. A warning about something going off names
    /// the shelf row it is about after a "?" (InventoryExpiryPushContent) - read as part of the id, that
    /// made the whole notification one that leads nowhere and could not even be marked read by tapping
    /// it. Caught on 2026-09-18, the day the row was added to that address.
    /// </summary>
    [Fact]
    public void And_neither_does_a_row_named_after_a_question_mark()
    {
        var inventoryId = Guid.NewGuid();

        Assert.Equal(
            new NotificationDestination(NotificationTarget.Inventory, inventoryId),
            NotificationDestination.Parse($"/inventory/{inventoryId}?highlight={Guid.NewGuid()}"));
    }

    /// <summary>The same for a destination that names nothing beyond its own page.</summary>
    [Fact]
    public void A_query_on_a_path_that_names_nothing_is_ignored_too()
        => Assert.Equal(
            new NotificationDestination(NotificationTarget.Map),
            NotificationDestination.Parse("/map?from=push"));
}
