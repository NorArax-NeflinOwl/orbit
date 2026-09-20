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
            subject: new TaskItemSubject(kind, location, linkedCalendarEventId));

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

    /// <summary>
    /// An entry that is a place and nothing else - "pick the keys up from the agent, here". It exists
    /// because an address used to be Calendar's alone to carry, so writing one down put a meeting in
    /// the calendar that nobody was going to.
    /// </summary>
    [Fact]
    public void A_location_entry_is_the_place_and_nothing_else()
    {
        var entry = Entry(TaskItemKind.Location, "  Przychodnia, ul. Długa 4  ");

        Assert.Equal(TaskItemKind.Location, entry.Kind);
        Assert.Equal("Przychodnia, ul. Długa 4", entry.Location);
        Assert.Null(entry.LinkedCalendarEventId);
    }

    /// <summary>
    /// And it keeps that place whatever it is handed beside it. Unlike a calendar entry, there is no
    /// event to defer to: an event id on a Location entry is dropped, so the place here is the only
    /// copy there is and losing it would leave the entry saying nothing at all.
    /// </summary>
    [Fact]
    public void A_location_entry_keeps_its_place_even_when_it_is_handed_an_event()
    {
        var entry = Entry(TaskItemKind.Location, "Przychodnia", Guid.NewGuid());

        Assert.Null(entry.LinkedCalendarEventId);
        Assert.Equal("Przychodnia", entry.Location);
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
            Guid.NewGuid(), "Dentist", dueDateUtc: null, isCompleted: false, linkedTaskListIds: null,
            new TaskItemReminders(
                Orbit.Core.Notifications.NotificationChannel.None, Daily: false,
                Orbit.Core.Notifications.NotificationChannel.None, default),
            new TaskItemSubject(TaskItemKind.Calendar, linkedCalendarEventId: eventId));

        Assert.Equal(TaskItemKind.Calendar, entry.Kind);
        Assert.Equal(eventId, entry.LinkedCalendarEventId);
    }

    /// <summary>
    /// The same rule about the same thing said twice, applied to when rather than where. A checklist
    /// line with a deadline, changed into an appointment, kept the deadline where no form showed it and
    /// nothing could clear it - and the calendar drew the entry twice, once as its event and once as a
    /// deadline of its own, with one tick behind both.
    /// </summary>
    [Fact]
    public void A_calendar_entry_has_no_deadline_of_its_own()
    {
        var entry = TaskItem.Create(
            "Dentist", DateTimeOffset.UtcNow.AddDays(3), isCompleted: false,
            subject: new TaskItemSubject(TaskItemKind.Calendar, linkedCalendarEventId: Guid.NewGuid()));

        Assert.Null(entry.DueDateUtc);
    }

    /// <summary>
    /// And it is the kind that decides, not the link: an entry switched to Calendar before its event
    /// has been made is on its way to being an appointment, and the form has already stopped asking it
    /// for a deadline.
    /// </summary>
    [Fact]
    public void A_calendar_entry_without_an_event_yet_has_none_either()
    {
        var entry = Entry(TaskItemKind.Calendar);

        Assert.Null(entry.DueDateUtc);
    }

    /// <summary>
    /// An errand about an amount keeps its date, though - "what do I need before Thursday" is asked
    /// against exactly that (RestockListSettings.OnlyLinkedWithDueDate). Nothing stands for it twice,
    /// so there is nothing to drop.
    /// </summary>
    [Fact]
    public void A_restock_errand_keeps_the_day_it_is_wanted_for()
    {
        var wanted = DateTimeOffset.UtcNow.AddDays(3);

        var entry = TaskItem.Create(
            "Flour", wanted, isCompleted: false,
            subject: new TaskItemSubject(TaskItemKind.Inventory, linkedInventoryItemId: Guid.NewGuid()));

        Assert.Equal(wanted, entry.DueDateUtc);
    }
}
