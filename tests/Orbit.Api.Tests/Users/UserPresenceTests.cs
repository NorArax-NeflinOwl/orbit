using Orbit.Core.Users;
using Xunit;

namespace Orbit.Api.Tests.Users;

/// <summary>
/// Pins how the four things a reader can see are derived from the two things that are stored - what
/// somebody chose, and when they were last heard from.
/// </summary>
public sealed class UserPresenceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Somebody_heard_from_just_now_is_available()
    {
        var presence = new UserPresence(PresenceAvailability.Available, Now.AddSeconds(-5));

        Assert.Equal(PresenceStatus.Available, presence.StatusAt(Now));
    }

    [Fact]
    public void Somebody_silent_for_a_minute_is_away()
    {
        var presence = new UserPresence(PresenceAvailability.Available, Now.AddMinutes(-1));

        Assert.Equal(PresenceStatus.Away, presence.StatusAt(Now));
    }

    [Fact]
    public void Somebody_silent_for_long_enough_is_offline()
    {
        var presence = new UserPresence(PresenceAvailability.Available, Now.AddMinutes(-5));

        Assert.Equal(PresenceStatus.Offline, presence.StatusAt(Now));
    }

    [Fact]
    public void An_account_never_heard_from_is_offline()
    {
        Assert.Equal(PresenceStatus.Offline, UserPresence.NeverSeen.StatusAt(Now));
    }

    [Fact]
    public void Choosing_not_to_be_disturbed_outranks_being_idle()
    {
        // Somebody who asked not to be interrupted is still not to be interrupted a minute later.
        var presence = new UserPresence(PresenceAvailability.DoNotDisturb, Now.AddMinutes(-2));

        Assert.Equal(PresenceStatus.DoNotDisturb, presence.StatusAt(Now));
    }

    [Fact]
    public void Leaving_outranks_choosing_not_to_be_disturbed()
    {
        // Red says "here, but busy". Somebody who set it and then left is not here at all, and showing
        // them as busy would promise there is someone to disturb.
        var presence = new UserPresence(PresenceAvailability.DoNotDisturb, Now.AddHours(-3));

        Assert.Equal(PresenceStatus.Offline, presence.StatusAt(Now));
    }

    [Fact]
    public void Choosing_a_status_counts_as_being_here()
    {
        var user = User.FromPersistence(
            Guid.NewGuid(), "someone@example.com", "someone", "Someone", "hash", Now.AddDays(-1), publicKeyBase64: null);

        user.SetAvailability(PresenceAvailability.DoNotDisturb, Now);

        // Without this, setting a status from a session that had gone quiet would leave the person
        // showing as offline until the next heartbeat - a status nobody would see.
        Assert.Equal(PresenceStatus.DoNotDisturb, user.Presence.StatusAt(Now));
    }
}
