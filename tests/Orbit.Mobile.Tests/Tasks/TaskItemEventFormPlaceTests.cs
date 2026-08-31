using Orbit.Contracts.Calendar;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens.Tasks;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Tasks;

/// <summary>
/// What a phone does to an appointment's place when it saves one.
///
/// Nothing on this screen can set a place - there is no map here - so the only question is whether it
/// leaves the one that is already there alone. It did not: every field of the event was read back so
/// that saving put the two in step, except this one, and saving therefore wrote a blank over a place
/// somebody had set on the web. Nobody touched the location; they changed the colour and pressed save.
/// </summary>
public sealed class TaskItemEventFormPlaceTests
{
    private static readonly EventLocationDto Somewhere = new("Przychodnia, Długa 4", 52.23, 21.01);

    [Fact]
    public void An_events_place_survives_being_saved_from_a_phone()
    {
        var form = TaskItemEventForm.For(AnEventAt(Somewhere), AnyLanguage());

        var saved = form.ToRequest("Dentist");

        Assert.NotNull(saved.Location);
        Assert.Equal("Przychodnia, Długa 4", saved.Location.Address);
        Assert.Equal(52.23, saved.Location.Latitude, precision: 2);
        Assert.Equal(21.01, saved.Location.Longitude, precision: 2);
    }

    /// <summary>The same, on the copy this phone keeps for itself - the two must not disagree.</summary>
    [Fact]
    public void The_copy_kept_on_the_phone_carries_the_place_too()
    {
        var form = TaskItemEventForm.For(AnEventAt(Somewhere), AnyLanguage());

        Assert.Equal(Somewhere, form.ToDetails("Dentist").Location);
    }

    /// <summary>An event that never had a place still has none, rather than one at Null Island.</summary>
    [Fact]
    public void An_event_with_no_place_still_has_none()
    {
        var form = TaskItemEventForm.For(AnEventAt(null), AnyLanguage());

        Assert.Null(form.ToRequest("Dentist").Location);
    }

    /// <summary>A brand new appointment has nothing to preserve.</summary>
    [Fact]
    public void A_new_appointment_starts_with_no_place()
        => Assert.Null(TaskItemEventForm.For(null, AnyLanguage()).ToRequest("Dentist").Location);

    private static Translations AnyLanguage() => new(new InMemoryLanguageStore());

    private static CalendarEventDetailsDto AnEventAt(EventLocationDto? location)
        => new(
            "Dentist", null, location, null,
            DateTimeOffset.Now.AddDays(1), DateTimeOffset.Now.AddDays(1).AddHours(1),
            IsAllDay: false, null, [], [], "None", "Push");
}
