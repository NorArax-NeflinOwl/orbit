namespace Orbit.Mobile.Google;

/// <summary>
/// Where this device keeps its answer about the Google extras - see <see cref="GoogleExtras"/>. An
/// interface for the reason every other device preference has one: the screens that read it are view
/// models this project tests.
/// </summary>
public interface IGoogleExtrasStore
{
    bool Read();

    void Write(bool isAllowed);
}

/// <summary>
/// Whether this phone offers the links that hand something to Google - an event to Google Calendar, a
/// place to Google Maps. On by default, and turned off from the account screen.
///
/// Kept on the device rather than on the account, as Orbit.Web keeps its own: it says what this phone
/// puts in front of whoever is holding it, and turning it off leaves a connected Google account
/// connected. The account's own half of the question - whether it may use the extras at all - is
/// <see cref="GoogleIntegrationAccess"/>.
/// </summary>
public sealed class GoogleExtras
{
    private readonly IGoogleExtrasStore _store;

    public GoogleExtras(IGoogleExtrasStore store) => _store = store;

    /// <summary>Raised when the answer changes, so a screen showing the links can drop them.</summary>
    public event EventHandler? Changed;

    public bool IsAllowedOnThisDevice
    {
        get => _store.Read();
        set
        {
            if (value == _store.Read())
            {
                return;
            }

            _store.Write(value);
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
