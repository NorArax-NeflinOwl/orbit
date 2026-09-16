using Orbit.Web.Services;
using Xunit;

namespace Orbit.Web.Tests.Services;

/// <summary>
/// Choosing several cards to act on together - the state behind the user's "select several notes,
/// lists, events or shelves and then file them into a folder or put them away". The rules that matter
/// are the ones a page would otherwise get subtly wrong: that choosing is a mode, that a card no longer
/// on the page stops counting, and that "all of them" is one press meaning both things.
/// </summary>
public sealed class PickedThingsTests
{
    private static readonly Guid First = Guid.NewGuid();
    private static readonly Guid Second = Guid.NewGuid();
    private static readonly Guid Third = Guid.NewGuid();

    [Fact]
    public void Nothing_is_chosen_until_the_page_is_choosing()
    {
        var picked = new PickedThings();

        picked.Toggle(First);

        Assert.False(picked.IsPicking);
        Assert.Equal(0, picked.Count);
    }

    [Fact]
    public void One_press_chooses_and_the_next_unchooses()
    {
        var picked = new PickedThings();
        picked.Start();

        picked.Toggle(First);
        Assert.True(picked.Holds(First));
        Assert.True(picked.HasAny);

        picked.Toggle(First);
        Assert.False(picked.Holds(First));
        Assert.False(picked.HasAny);
    }

    /// <summary>
    /// Leaving the mode forgets the selection: coming back to a page still holding what was chosen
    /// three screens ago would act on cards nobody remembers choosing.
    /// </summary>
    [Fact]
    public void Stopping_forgets_what_was_chosen()
    {
        var picked = new PickedThings();
        picked.Start();
        picked.Toggle(First);

        picked.Stop();

        Assert.False(picked.IsPicking);
        Assert.Equal(0, picked.Count);
    }

    /// <summary>And starting again begins from nothing, rather than from the last selection.</summary>
    [Fact]
    public void Starting_again_begins_from_nothing()
    {
        var picked = new PickedThings();
        picked.Start();
        picked.Toggle(First);
        picked.Stop();

        picked.Start();

        Assert.Equal(0, picked.Count);
    }

    [Fact]
    public void All_of_them_chooses_every_card_on_the_page()
    {
        var picked = new PickedThings();
        picked.Start();

        picked.ToggleAll([First, Second, Third]);

        Assert.Equal(3, picked.Count);
    }

    /// <summary>The same press again, with all of them chosen, unchooses them - it is one button.</summary>
    [Fact]
    public void All_of_them_pressed_twice_leaves_nothing_chosen()
    {
        var picked = new PickedThings();
        picked.Start();
        picked.ToggleAll([First, Second]);

        picked.ToggleAll([First, Second]);

        Assert.Equal(0, picked.Count);
    }

    /// <summary>
    /// With only some chosen it chooses the rest rather than clearing: somebody who has picked two of
    /// five and presses "all" means all five, not none.
    /// </summary>
    [Fact]
    public void All_of_them_fills_in_the_rest_when_only_some_are_chosen()
    {
        var picked = new PickedThings();
        picked.Start();
        picked.Toggle(First);

        picked.ToggleAll([First, Second, Third]);

        Assert.Equal(3, picked.Count);
    }

    /// <summary>
    /// What a folder tab changing leaves behind. Without this the bar went on counting cards nobody
    /// could see, and the next press acted on them.
    /// </summary>
    [Fact]
    public void A_card_no_longer_on_the_page_stops_counting()
    {
        var picked = new PickedThings();
        picked.Start();
        picked.ToggleAll([First, Second]);

        picked.KeepOnly([First, Third]);

        Assert.Equal([First], picked.Ids);
    }

    /// <summary>Narrowing to nothing leaves the mode on: the way out is the reader's to press.</summary>
    [Fact]
    public void Narrowing_to_nothing_still_leaves_the_page_choosing()
    {
        var picked = new PickedThings();
        picked.Start();
        picked.Toggle(First);

        picked.KeepOnly([]);

        Assert.True(picked.IsPicking);
        Assert.False(picked.HasAny);
    }
}
