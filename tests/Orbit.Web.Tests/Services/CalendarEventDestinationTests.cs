using Orbit.Contracts.Tasks;
using Orbit.Web.Services;
using Xunit;

namespace Orbit.Web.Tests.Services;

/// <summary>
/// Where pressing an event leads. The rule is one place because an event is reached from three - the
/// calendar's list, its day grid, and the dashboard - and each of those used to answer differently.
/// </summary>
public sealed class CalendarEventDestinationTests
{
    [Fact]
    public void An_event_a_list_raised_opens_as_that_lists_entry()
    {
        var eventId = Guid.NewGuid();
        var entry = AnEntry("Dentist") with { LinkedCalendarEventId = eventId };
        var taskList = AList("Health", entry);

        var destination = CalendarEventDestination.For(eventId, [taskList]);

        // The list is where the work is done; the event form is a different thing from ticking a step off.
        Assert.Equal($"/tasks/{taskList.Id}/items/{entry.Id}", destination);
    }

    [Fact]
    public void An_event_nothing_points_at_opens_as_itself()
    {
        var eventId = Guid.NewGuid();

        var destination = CalendarEventDestination.For(eventId, [AList("Health", AnEntry("Buy plasters"))]);

        Assert.Equal($"/calendar/{eventId}", destination);
    }

    /// <summary>A page that has not read the lists yet still has to send a press somewhere.</summary>
    [Fact]
    public void With_no_lists_read_yet_the_event_still_opens()
    {
        var eventId = Guid.NewGuid();

        Assert.Equal($"/calendar/{eventId}", CalendarEventDestination.For(eventId, null));
    }

    private static TaskDto AList(string title, params TaskItemDto[] items)
        => new(
            Guid.NewGuid(), title, items, IsCompleted: false, IsGroup: false, IsPrivate: false, EncryptedContent: null,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            IsShared: false, SharedByUserName: null, AccessLevel: "CanEdit", OriginalOwnerUserId: null);

    private static TaskItemDto AnEntry(string description)
        => new(
            Guid.NewGuid(), description, DueDateUtc: null, IsCompleted: false, LinkedTaskListId: null,
            OverdueNotificationChannel: "None", RemindDaily: false,
            DailyReminderNotificationChannel: "None", DailyReminderTimeOfDay: default);
}
