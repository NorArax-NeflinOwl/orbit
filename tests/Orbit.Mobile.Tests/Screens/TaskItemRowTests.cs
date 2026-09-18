using Orbit.Contracts.Calendar;
using Orbit.Contracts.Tasks;
using Orbit.Core.Tasks;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens.Tasks;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Screens;

/// <summary>
/// What one entry's row says under its words. The interesting case is an entry tied to an appointment:
/// the day and the hour live on the event, so the entry's own due date is whatever it was when the
/// entry was made, and an appointment moved in a browser leaves the two saying different things. The
/// row follows the event, which is what Orbit.Web's entry page has always done.
/// </summary>
public sealed class TaskItemRowTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void An_entry_tied_to_an_appointment_says_when_the_appointment_is()
    {
        var translations = new Translations(new InMemoryLanguageStore());
        var appointment = Appointment(
            from: new DateTimeOffset(2026, 9, 21, 11, 0, 0, TimeSpan.Zero),
            to: new DateTimeOffset(2026, 9, 21, 11, 45, 0, TimeSpan.Zero));

        var row = TaskItemRow.From(
            AnAppointmentEntry(due: new DateTimeOffset(2026, 9, 17, 7, 0, 0, TimeSpan.Zero)),
            translations, Now, appointment: appointment);

        Assert.Equal(EventWhenReads(appointment, translations), row.Detail);
        // The entry's own deadline, the day it was made under, is not what the row says any more.
        Assert.DoesNotContain("17", row.Detail);
    }

    /// <summary>A deadline with no appointment behind it still has its own date, and that is what it shows.</summary>
    [Fact]
    public void An_entry_with_only_a_deadline_still_says_that()
    {
        var translations = new Translations(new InMemoryLanguageStore());

        var row = TaskItemRow.From(
            AnAppointmentEntry(due: new DateTimeOffset(2026, 9, 17, 7, 0, 0, TimeSpan.Zero)) with
            {
                Kind = nameof(TaskItemKind.Checklist)
            },
            translations, Now);

        Assert.Contains("17", row.Detail);
    }

    /// <summary>
    /// An appointment is late once it has ended, not once it has begun - so one running right now is
    /// not overdue, however long ago the entry's own deadline was.
    /// </summary>
    [Fact]
    public void An_appointment_still_running_is_not_overdue()
    {
        var row = TaskItemRow.From(
            AnAppointmentEntry(due: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)),
            new Translations(new InMemoryLanguageStore()), Now,
            appointment: Appointment(Now.AddMinutes(-15), Now.AddMinutes(45)));

        Assert.False(row.IsOverdue);
    }

    [Fact]
    public void An_appointment_that_has_ended_is_overdue()
    {
        var row = TaskItemRow.From(
            AnAppointmentEntry(due: null),
            new Translations(new InMemoryLanguageStore()), Now,
            appointment: Appointment(Now.AddHours(-3), Now.AddHours(-2)));

        Assert.True(row.IsOverdue);
    }

    private static string EventWhenReads(CalendarEventDetailsDto appointment, Translations translations)
        => Orbit.Mobile.Screens.Calendar.EventWhen.Reads(appointment, translations);

    private static CalendarEventDetailsDto Appointment(DateTimeOffset from, DateTimeOffset to)
        => new(
            "Dentist", Description: null, Location: null, Color: null, from, to, IsAllDay: false,
            Recurrence: null, Guests: [], ReminderMinutesBeforeStart: [], ReminderNotificationChannel: "None");

    private static TaskItemDto AnAppointmentEntry(DateTimeOffset? due)
        => new(
            Guid.NewGuid(), "Dentist", due, IsCompleted: false, LinkedTaskListId: null,
            OverdueNotificationChannel: "None", RemindDaily: false, DailyReminderNotificationChannel: "None",
            DailyReminderTimeOfDay: new TimeOnly(9, 0), Kind: nameof(TaskItemKind.Calendar));
}
