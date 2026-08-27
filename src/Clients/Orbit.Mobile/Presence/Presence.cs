using Orbit.Mobile.Sync;

namespace Orbit.Mobile.Presence;

/// <summary>What the dot on the avatar is saying.</summary>
public enum PresenceAppearance
{
    /// <summary>Green: reachable and using the app.</summary>
    Active,

    /// <summary>Amber: reachable, but nothing has been touched for a while.</summary>
    Idle,

    /// <summary>Red: reachable, and has asked not to be disturbed.</summary>
    Unavailable,

    /// <summary>Grey: not reachable at all, whatever the reader chose.</summary>
    Offline
}

/// <summary>What the reader has chosen, as opposed to what the app worked out.</summary>
public enum ChosenAvailability
{
    Available,
    Unavailable
}

/// <summary>
/// Whether the reader is reachable, and how they want to appear.
///
/// Three inputs, one answer. Being offline beats everything: a status nobody can see is not a status, so
/// a phone with no connection is grey however available its owner feels. Then the reader's own choice,
/// which is a decision and outranks anything inferred. Only then the guess - idle after a minute of not
/// touching anything.
///
/// <b>Local to this device.</b> Orbit's server has no notion of presence, so nothing here reaches
/// anybody else yet; the dot tells the reader what they have set, not what a contact sees.
/// </summary>
public sealed class Presence
{
    /// <summary>
    /// How long without a sign of life counts as idle. A minute is short for a desktop and about right
    /// for a phone, which is picked up and put down constantly.
    /// </summary>
    public static readonly TimeSpan IdleAfter = TimeSpan.FromMinutes(1);

    private readonly INetworkStatus _networkStatus;
    private readonly IPresenceStore _store;
    private readonly TimeProvider _timeProvider;
    private DateTimeOffset _lastActiveAtUtc;

    public Presence(INetworkStatus networkStatus, IPresenceStore store, TimeProvider timeProvider)
    {
        _networkStatus = networkStatus;
        _store = store;
        _timeProvider = timeProvider;
        _lastActiveAtUtc = timeProvider.GetUtcNow();
        Chosen = store.Read();
    }

    /// <summary>Raised when <see cref="Appearance"/> may have changed, so the bar can redraw its dot.</summary>
    public event EventHandler? Changed;

    /// <summary>
    /// What the reader picked. Read from storage once at construction rather than on every glance, and
    /// written back when it changes - see <see cref="IPresenceStore"/>.
    /// </summary>
    public ChosenAvailability Chosen { get; private set; }

    public PresenceAppearance Appearance
    {
        get
        {
            if (!_networkStatus.IsOnline)
            {
                return PresenceAppearance.Offline;
            }

            if (Chosen == ChosenAvailability.Unavailable)
            {
                return PresenceAppearance.Unavailable;
            }

            return _timeProvider.GetUtcNow() - _lastActiveAtUtc >= IdleAfter
                ? PresenceAppearance.Idle
                : PresenceAppearance.Active;
        }
    }

    public void Choose(ChosenAvailability availability)
    {
        Chosen = availability;
        _store.Write(availability);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// A sign of life. Called when the reader moves around the app or brings it back to the front -
    /// deliberately not on every frame or scroll: this is about "has this phone been put down", and
    /// reading one long note is closer to put-down than to gone.
    /// </summary>
    public void MarkActive()
    {
        _lastActiveAtUtc = _timeProvider.GetUtcNow();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Re-evaluated on a timer, because becoming idle is the one change nothing triggers.</summary>
    public void ReconsiderIdleness() => Changed?.Invoke(this, EventArgs.Empty);
}
