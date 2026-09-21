using Orbit.Core.Calendar;
using Orbit.Core.Calendar.Reminders;
using Orbit.Core.Inventories.ExpiryReminders;
using Orbit.Core.Notifications;
using Xunit;

namespace Orbit.Api.Tests.Notifications;

/// <summary>
/// The calendar's reminders and the shelves' expiry warnings gather what falls due for one reader in
/// the same poll, the way the task services do (see OneNoticeForWhatFellDueTogetherTests): two
/// appointments at nine, or three things going off on the same day, used to cost the reader every
/// banner but the last, since the web shows the newest entry only and both clients keep a gap between
/// banners.
/// </summary>
public sealed class OneNoticeForEventsAndShelvesTests
{
    private static readonly Guid Fridge = Guid.NewGuid();
    private static readonly Guid Pantry = Guid.NewGuid();
    private static readonly DateTimeOffset AtNine = new(2026, 9, 22, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Two_appointments_at_nine_are_named_in_one_reminder()
    {
        var payload = EventReminderPushContent.Build([AnAppointment("Dentist", 0), AnAppointment("Standup", 15)]);

        Assert.Equal("Upcoming events", payload.Title);
        Assert.Contains("Dentist", payload.Body);
        Assert.Contains("Standup", payload.Body);
    }

    /// <summary>There is no page for "these two appointments", so the calendar is where it lands.</summary>
    [Fact]
    public void A_reminder_about_two_appointments_leads_to_the_calendar()
    {
        var payload = EventReminderPushContent.Build([AnAppointment("Dentist", 0), AnAppointment("Standup", 15)]);

        Assert.Equal("/calendar", payload.Url);
    }

    /// <summary>One appointment says what it always said, lead time and all.</summary>
    [Fact]
    public void One_appointment_still_speaks_for_itself()
    {
        var dentist = AnAppointment("Dentist", 15);

        var payload = EventReminderPushContent.Build([dentist]);

        AssertSaysTheSame(EventReminderPushContent.Build(dentist.Details, dentist.CalendarEventId, 15), payload);
    }

    [Fact]
    public void The_appointments_e_mail_says_how_many_and_when_each_starts()
    {
        var (subject, body) = EventReminderEmailContent.Build([AnAppointment("Dentist", 0), AnAppointment("Standup", 15)]);

        Assert.Equal("Reminder: 2 upcoming events", subject);
        Assert.Contains("\"Dentist\" starts now.", body);
        Assert.Contains("\"Standup\" starts in 15 min.", body);
    }

    [Fact]
    public void Two_things_going_off_are_named_in_one_warning_with_their_dates()
    {
        var payload = InventoryExpiryPushContent.Build([AThing("Milk", Fridge, 1), AThing("Eggs", Fridge, 2)]);

        Assert.Equal("Things expiring soon", payload.Title);
        Assert.Contains($"Milk ({AtNine.AddDays(1).LocalDateTime:dd.MM.yyyy})", payload.Body);
        Assert.Contains($"Eggs ({AtNine.AddDays(2).LocalDateTime:dd.MM.yyyy})", payload.Body);
    }

    /// <summary>
    /// The storage they are all on, with no row picked out: a mark on one row would say the other did
    /// not matter.
    /// </summary>
    [Fact]
    public void A_warning_about_one_storage_leads_to_it_without_marking_a_row()
    {
        var payload = InventoryExpiryPushContent.Build([AThing("Milk", Fridge, 1), AThing("Eggs", Fridge, 2)]);

        Assert.Equal($"/inventory/{Fridge}", payload.Url);
    }

    [Fact]
    public void A_warning_about_two_storages_leads_to_the_storages()
    {
        var payload = InventoryExpiryPushContent.Build([AThing("Milk", Fridge, 1), AThing("Rice", Pantry, 2)]);

        Assert.Equal("/inventory", payload.Url);
    }

    /// <summary>One thing keeps the warning it always had, down to the row it marks.</summary>
    [Fact]
    public void One_thing_still_speaks_for_itself()
    {
        var milk = AThing("Milk", Fridge, 1);

        var payload = InventoryExpiryPushContent.Build([milk]);

        AssertSaysTheSame(InventoryExpiryPushContent.Build(milk), payload);
    }

    [Fact]
    public void The_expiry_e_mail_says_how_many_and_names_each_of_them()
    {
        var (subject, body) = InventoryExpiryEmailContent.Build([AThing("Milk", Fridge, 1), AThing("Rice", Pantry, 2)]);

        Assert.Equal("2 things expiring soon", subject);
        Assert.Contains("\"Milk\"", body);
        Assert.Contains("\"Rice\"", body);
    }

    /// <summary>Compared by what it says: the payload carries its arguments as lists, which a record compares by reference.</summary>
    private static void AssertSaysTheSame(PushNotificationPayload expected, PushNotificationPayload actual)
    {
        Assert.Equal(expected.Title, actual.Title);
        Assert.Equal(expected.Body, actual.Body);
        Assert.Equal(expected.Url, actual.Url);
    }

    private static EventReminderOccurrence AnAppointment(string title, int minutesBeforeStart)
        => new(
            new CalendarEventDetails(
                title, null, null, null, AtNine, AtNine.AddHours(1), false, null, [], [], NotificationChannel.Both),
            Guid.NewGuid(), minutesBeforeStart);

    private static DueExpiryReminder AThing(string name, Guid inventoryId, int daysFromNow)
        => new(
            Guid.NewGuid(), inventoryId, Guid.NewGuid(), name, AtNine.AddDays(daysFromNow), NotificationChannel.Both,
            Quantity: 1);
}
