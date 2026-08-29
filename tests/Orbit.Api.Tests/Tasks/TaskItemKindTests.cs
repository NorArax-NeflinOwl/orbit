using Orbit.Core.Tasks;
using Xunit;

namespace Orbit.Api.Tests.Tasks;

/// <summary>
/// What one entry on a list is, and the one thing that kind brings with it. The kind sits on the entry
/// rather than on the list because a day's plan holds two errands and an appointment - so most of this
/// is about a place belonging only where it means something.
/// </summary>
public sealed class TaskItemKindTests
{
    private static TaskItem Entry(
        TaskItemKind kind = TaskItemKind.Checklist, string location = "", Guid? linkedCalendarEventId = null)
        => TaskItem.Create(
            "Dentist", dueDateUtc: null, isCompleted: false,
            kind: kind, location: location, linkedCalendarEventId: linkedCalendarEventId);

    [Fact]
    public void An_entry_is_an_ordinary_one_unless_it_says_otherwise()
    {
        var entry = Entry();

        Assert.Equal(TaskItemKind.Checklist, entry.Kind);
        Assert.Equal(string.Empty, entry.Location);
        Assert.Null(entry.LinkedCalendarEventId);
    }

    [Fact]
    public void A_calendar_entry_keeps_where_it_happens()
    {
        var entry = Entry(TaskItemKind.Calendar, "  Przychodnia, ul. Długa 4  ");

        Assert.Equal(TaskItemKind.Calendar, entry.Kind);
        // Trimmed, since it is written by hand and read back beside a description that is trimmed too.
        Assert.Equal("Przychodnia, ul. Długa 4", entry.Location);
    }

    [Fact]
    public void An_ordinary_entry_has_nowhere_to_be_even_if_it_is_told_one()
    {
        Assert.Equal(string.Empty, Entry(TaskItemKind.Checklist, "Przychodnia").Location);
    }

    [Fact]
    public void An_ordinary_entry_is_tied_to_no_event_even_if_it_is_handed_one()
    {
        Assert.Null(Entry(TaskItemKind.Checklist, linkedCalendarEventId: Guid.NewGuid()).LinkedCalendarEventId);
    }

    [Fact]
    public void An_entry_tied_to_an_event_keeps_no_place_of_its_own()
    {
        var entry = Entry(TaskItemKind.Calendar, "Przychodnia", Guid.NewGuid());

        // The event holds the place. Storing it twice is how the two come to disagree, which is the
        // whole reason the link exists.
        Assert.NotNull(entry.LinkedCalendarEventId);
        Assert.Equal(string.Empty, entry.Location);
    }

    [Fact]
    public void An_entry_read_back_from_storage_carries_all_three()
    {
        var eventId = Guid.NewGuid();

        var entry = TaskItem.FromPersistence(
            Guid.NewGuid(), "Dentist", dueDateUtc: null, isCompleted: false, linkedTaskListId: null,
            Orbit.Core.Notifications.NotificationChannel.None, remindDaily: false,
            Orbit.Core.Notifications.NotificationChannel.None, dailyReminderTimeOfDay: default,
            TaskItemKind.Calendar, location: "", linkedCalendarEventId: eventId);

        Assert.Equal(TaskItemKind.Calendar, entry.Kind);
        Assert.Equal(eventId, entry.LinkedCalendarEventId);
    }
}
