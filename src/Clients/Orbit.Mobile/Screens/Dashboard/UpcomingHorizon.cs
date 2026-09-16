namespace Orbit.Mobile.Screens.Dashboard;

/// <summary>
/// Where this device keeps how far ahead the Upcoming card looks - see <see cref="UpcomingHorizon"/>.
/// An interface for the reason every device preference here has one: the screens that read it are view
/// models this project tests.
/// </summary>
public interface IUpcomingHorizonStore
{
    /// <summary>The horizon stored in days, or null while nothing has been - which means the default.</summary>
    int? ReadDays();

    void WriteDays(int days);
}

/// <summary>
/// How far ahead the dashboard's Upcoming card looks, in days. A week by default: the card holds six
/// rows and is glanced at, so a year of appointments turned it into a list of everything that will ever
/// happen with next Tuesday somewhere inside it. The calendar is where a longer view is read, and it is
/// one press away from the card's own name.
///
/// Kept on the device, as Orbit.Web keeps its own (DevicePreferences.UpcomingDays), and set on the
/// account screen's Preferences tab. The two lists of horizons are written out on both sides rather than
/// shared, which is what EntryFilling.EntryKinds does beside DevicePreferences.EntryKinds - a browser
/// preference and a phone preference are stored in different places and answer to different screens.
/// </summary>
public sealed class UpcomingHorizon(IUpcomingHorizonStore store)
{
    /// <summary>A week, which is the horizon a card of six rows can hold without becoming a list.</summary>
    public const int DefaultDays = 7;

    /// <summary>
    /// What the Preferences tab offers, shortest first, with everything last. Listed rather than typed
    /// in: a horizon is a choice between a few useful answers, not a number somebody has an opinion
    /// about to the day. The same five Orbit.Web offers, so a reader who uses both meets one list.
    /// </summary>
    public static readonly IReadOnlyList<int> Horizons = [1, 7, 30, 90, 0];

    /// <summary>
    /// Zero means no horizon at all - everything, which is what the card used to do and is kept as a
    /// choice for somebody whose calendar is thin enough to want it. Only a number <see cref="Horizons"/>
    /// offers is taken back: anything else stored is a value from a version that offered it, or
    /// something nobody wrote, and neither is a horizon to draw a card with.
    /// </summary>
    public int Days => store.ReadDays() is { } days && Horizons.Contains(days) ? days : DefaultDays;

    public void SetDays(int days) => store.WriteDays(Horizons.Contains(days) ? days : DefaultDays);

    /// <summary>
    /// Whether something coming up is near enough for the card to say so.
    ///
    /// Measured from the start of today in the reader's own time zone rather than from this moment, so
    /// something happening this morning is still on a card read this afternoon - a horizon that quietly
    /// dropped what is happening today would be worse than no horizon. Orbit.Web measures it the same
    /// way, in Dashboard.IsInsideTheHorizon.
    /// </summary>
    public bool Holds(DateTimeOffset at, DateTimeOffset now, TimeZoneInfo zone)
    {
        var days = Days;
        if (days <= 0)
        {
            return true;
        }

        return TimeZoneInfo.ConvertTime(at, zone).Date <= TimeZoneInfo.ConvertTime(now, zone).Date.AddDays(days);
    }
}
