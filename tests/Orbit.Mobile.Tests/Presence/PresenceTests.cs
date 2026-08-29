using Microsoft.Extensions.Time.Testing;
using Orbit.Mobile.Presence;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Presence;

/// <summary>
/// The dot on the avatar. Three inputs decide it and their order is the whole design: being unreachable
/// beats a choice, and a choice beats a guess.
/// </summary>
public sealed class PresenceTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-08-27T09:00:00Z");

    [Fact]
    public void Somebody_using_the_app_is_green()
    {
        var context = new PresenceContext();

        Assert.Equal(PresenceAppearance.Active, context.Presence.Appearance);
    }

    [Fact]
    public void Somebody_who_has_not_touched_anything_for_a_minute_is_amber()
    {
        var context = new PresenceContext();

        context.Clock.Advance(Orbit.Mobile.Presence.Presence.IdleAfter);

        Assert.Equal(PresenceAppearance.Idle, context.Presence.Appearance);
    }

    [Fact]
    public void Touching_anything_makes_them_green_again()
    {
        var context = new PresenceContext();
        context.Clock.Advance(TimeSpan.FromMinutes(5));

        context.Presence.MarkActive();

        Assert.Equal(PresenceAppearance.Active, context.Presence.Appearance);
    }

    [Fact]
    public void Choosing_to_be_unavailable_is_red_even_while_using_the_app()
    {
        // A decision outranks anything the app inferred - that is the point of being able to make it.
        var context = new PresenceContext();

        context.Presence.Choose(ChosenAvailability.Unavailable);

        Assert.Equal(PresenceAppearance.Unavailable, context.Presence.Appearance);
    }

    [Fact]
    public void Being_unavailable_outranks_being_idle()
    {
        var context = new PresenceContext();
        context.Presence.Choose(ChosenAvailability.Unavailable);

        context.Clock.Advance(TimeSpan.FromHours(1));

        Assert.Equal(PresenceAppearance.Unavailable, context.Presence.Appearance);
    }

    [Fact]
    public void No_connection_is_grey_whatever_the_reader_chose()
    {
        // A status nobody can see is not a status. Showing red while unreachable would claim the app is
        // telling somebody something it cannot tell them.
        var context = new PresenceContext(isOnline: false);
        context.Presence.Choose(ChosenAvailability.Unavailable);

        Assert.Equal(PresenceAppearance.Offline, context.Presence.Appearance);
    }

    [Fact]
    public void A_choice_is_remembered_across_launches()
    {
        // Do not disturb is a deliberate decision; an app that quietly forgets it announces somebody
        // exactly when they asked it not to.
        var store = new InMemoryPresenceStore();
        new Orbit.Mobile.Presence.Presence(FixedNetworkStatus.Online, store, new FakeTimeProvider(Now))
            .Choose(ChosenAvailability.Unavailable);

        var afterRestart = new Orbit.Mobile.Presence.Presence(
            FixedNetworkStatus.Online, store, new FakeTimeProvider(Now));

        Assert.Equal(ChosenAvailability.Unavailable, afterRestart.Chosen);
    }

    [Fact]
    public void A_fresh_install_is_available()
    {
        var context = new PresenceContext();

        Assert.Equal(ChosenAvailability.Available, context.Presence.Chosen);
    }

    private sealed class PresenceContext
    {
        public PresenceContext(bool isOnline = true)
        {
            Clock = new FakeTimeProvider(Now);
            Presence = new Orbit.Mobile.Presence.Presence(
                isOnline ? FixedNetworkStatus.Online : FixedNetworkStatus.Offline,
                new InMemoryPresenceStore(), Clock);
        }

        public FakeTimeProvider Clock { get; }

        public Orbit.Mobile.Presence.Presence Presence { get; }
    }
}
