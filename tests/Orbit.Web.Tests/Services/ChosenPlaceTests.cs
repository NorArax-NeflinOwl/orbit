using Orbit.Web.Components;
using Orbit.Web.Services;
using Xunit;

namespace Orbit.Web.Tests.Services;

/// <summary>
/// The handover between the map and the form it sends somebody to.
///
/// Taking rather than reading is the whole rule: a place read twice would fill the box again on a
/// later visit to the same form, with somewhere the reader looked at once and has no memory of
/// choosing - which is worse than an empty box, because it looks deliberate.
/// </summary>
public sealed class ChosenPlaceTests
{
    private static readonly PickedPlace Somewhere = new("Długa 4, Warszawa", 52.2497, 21.0122);

    [Fact]
    public void Nothing_is_waiting_until_somewhere_is_chosen()
    {
        var chosen = new ChosenPlace();

        Assert.False(chosen.IsWaiting);
        Assert.Null(chosen.Take());
    }

    [Fact]
    public void What_was_held_is_what_comes_back()
    {
        var chosen = new ChosenPlace();
        chosen.Hold(Somewhere);

        Assert.True(chosen.IsWaiting);
        Assert.Equal(Somewhere, chosen.Take());
    }

    [Fact]
    public void Taking_it_is_the_end_of_it()
    {
        var chosen = new ChosenPlace();
        chosen.Hold(Somewhere);
        chosen.Take();

        Assert.False(chosen.IsWaiting);
        Assert.Null(chosen.Take());
    }

    /// <summary>Choosing somewhere else before the first was collected replaces it rather than queueing.</summary>
    [Fact]
    public void A_second_choice_replaces_the_first()
    {
        var chosen = new ChosenPlace();
        chosen.Hold(Somewhere);
        var elsewhere = new PickedPlace("Rynek 1, Kraków", 50.0616, 19.9373);
        chosen.Hold(elsewhere);

        Assert.Equal(elsewhere, chosen.Take());
    }
}
