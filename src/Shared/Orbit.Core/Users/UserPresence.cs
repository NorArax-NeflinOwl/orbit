namespace Orbit.Core.Users;

/// <summary>The part of presence a person sets themselves - everything else is derived from it.</summary>
public enum PresenceAvailability
{
    Available,

    /// <summary>Signed in and asking not to be interrupted - shown in red, not as absence.</summary>
    DoNotDisturb
}

/// <summary>What everyone else sees: the chosen availability, tempered by how recently the person was actually here.</summary>
public enum PresenceStatus
{
    Available,

    /// <summary>Nothing from this person for a minute or so - still here, just not at the keyboard.</summary>
    Away,
    DoNotDisturb,

    /// <summary>Nothing from this person for long enough that they are not reachable at all.</summary>
    Offline
}

/// <summary>
/// Somebody's availability and when they were last seen, kept together because neither one alone says
/// what to show next to their name: a person who chose "do not disturb" and then closed their laptop is
/// offline, not busy, and a person who chose nothing at all is available only while they are actually
/// there. <see cref="StatusAt"/> is the only place that resolves the two.
/// </summary>
public sealed record UserPresence(PresenceAvailability Availability, DateTimeOffset? LastSeenAtUtc)
{
    /// <summary>Where an account starts: available in principle, never seen in practice, so it reads as offline.</summary>
    public static readonly UserPresence NeverSeen = new(PresenceAvailability.Available, null);

    /// <summary>How long a silence has to last before somebody counts as away rather than at the keyboard.</summary>
    public static readonly TimeSpan AwayAfter = TimeSpan.FromMinutes(1);

    /// <summary>
    /// How long a silence has to last before somebody counts as gone. Comfortably longer than the
    /// client's heartbeat interval, so one missed heartbeat - a slow network, a laptop lid - does not
    /// put a present person offline.
    /// </summary>
    public static readonly TimeSpan OfflineAfter = TimeSpan.FromMinutes(5);

    public UserPresence SeenAt(DateTimeOffset nowUtc) => this with { LastSeenAtUtc = nowUtc };

    public UserPresence WithAvailability(PresenceAvailability availability) => this with { Availability = availability };

    /// <summary>
    /// Absence outranks choice: someone who set "do not disturb" and then left is offline, because
    /// showing them as busy would promise they are there to be disturbed in the first place.
    /// </summary>
    public PresenceStatus StatusAt(DateTimeOffset nowUtc)
    {
        if (LastSeenAtUtc is not { } lastSeen || nowUtc - lastSeen >= OfflineAfter)
        {
            return PresenceStatus.Offline;
        }

        if (Availability == PresenceAvailability.DoNotDisturb)
        {
            return PresenceStatus.DoNotDisturb;
        }

        return nowUtc - lastSeen >= AwayAfter ? PresenceStatus.Away : PresenceStatus.Available;
    }
}
