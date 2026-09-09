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

    /// <summary>
    /// What a contact's card shows, and all of last-seen that leaves the server - the exact second
    /// somebody's browser last beat is not a thing anybody needs to know about them.
    /// </summary>
    [Fact]
    public void What_a_contact_is_told_is_the_minute_somebody_was_last_here()
    {
        var presence = new UserPresence(
            PresenceAvailability.Available, new DateTimeOffset(2026, 8, 1, 9, 47, 53, TimeSpan.Zero));

        Assert.Equal(new DateTimeOffset(2026, 8, 1, 9, 47, 0, TimeSpan.Zero), presence.LastSeenToTheMinuteUtc);
    }

    /// <summary>
    /// And the stored instant keeps its seconds, because the away threshold is measured against it:
    /// rounded down, somebody last seen at 10:00:59 would turn "away" five seconds later, having been
    /// at the keyboard the whole time.
    /// </summary>
    [Fact]
    public void Rounding_what_is_shown_does_not_round_what_status_is_measured_against()
    {
        var lastSeen = new DateTimeOffset(2026, 8, 1, 10, 0, 59, TimeSpan.Zero);
        var presence = new UserPresence(PresenceAvailability.Available, lastSeen);

        Assert.Equal(lastSeen, presence.LastSeenAtUtc);
        Assert.Equal(PresenceStatus.Available, presence.StatusAt(lastSeen.AddSeconds(5)));
    }

    [Fact]
    public void An_account_nobody_has_ever_seen_has_no_minute_to_show()
    {
        Assert.Null(UserPresence.NeverSeen.LastSeenToTheMinuteUtc);
    }
}
