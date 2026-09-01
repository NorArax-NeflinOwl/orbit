using Orbit.Contracts.Calendar;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens.Tasks;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Tasks;

/// <summary>
/// What a phone does to an appointment's place when it saves one.
///
/// The worry these started with: every field of the event was read back so that saving put the two in
/// step, except this one, so saving wrote a blank over a place somebody had set in the browser. Nobody
/// touched the location; they changed the colour and pressed save.
///
/// The answer moved a level up. The place is not kept inside the form any more - it is carried by the
/// editor, filled from the event when the entry opens - so it makes the whole round trip rather than
/// only surviving. These check both halves of that: untouched it comes back, and emptied it goes.
/// </summary>
public sealed class TaskItemEventFormPlaceTests
{
    private static readonly EventLocationDto Somewhere = new("Przychodnia, Długa 4", 52.23, 21.01);

    [Fact]
    public void An_events_place_is_sent_back_when_the_editor_still_holds_it()
    {
        var form = TaskItemEventForm.For(AnEventAt(Somewhere), AnyLanguage());

        var saved = form.ToRequest("Dentist", new EventPlace("Przychodnia, Długa 4", 52.23, 21.01));

        Assert.NotNull(saved.Location);
        Assert.Equal("Przychodnia, Długa 4", saved.Location.Address);
        Assert.Equal(52.23, saved.Location.Latitude, precision: 2);
    }

    /// <summary>
    /// And emptying the box removes it, which is what emptying a box means. Holding the old place
    /// regardless would make a place impossible to take off an appointment from a phone.
    /// </summary>
    [Fact]
    public void Emptying_the_box_takes_the_place_off()
    {
        var form = TaskItemEventForm.For(AnEventAt(Somewhere), AnyLanguage());

        Assert.Null(form.ToRequest("Dentist", EventPlace.Nowhere).Location);
    }

    /// <summary>A name with no point behind it cannot be stored - an event holds a point first.</summary>
    [Fact]
    public void A_name_with_nowhere_behind_it_is_not_a_place()
    {
        Assert.False(new EventPlace("somewhere nobody can find").CanBeSaved);
        Assert.Null(new EventPlace("somewhere nobody can find").ToRequest());
    }

    private static CalendarEventDetailsDto AnEventAt(EventLocationDto? location)
        => new(
            "Dentist", null, location, null,
            DateTimeOffset.Parse("2026-09-03T14:30:00Z"), DateTimeOffset.Parse("2026-09-03T15:00:00Z"),
            false, null, [], [], "None", "Push");

    private static Translations AnyLanguage() => new(new InMemoryLanguageStore());
}
