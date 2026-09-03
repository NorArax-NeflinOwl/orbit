using Orbit.Mobile.Google;

namespace Orbit.Mobile.Tests.TestDoubles;

/// <summary>
/// Whether this "device" offers the Google links, held for as long as one test runs. On to begin with,
/// as a phone nobody has answered for is - see PreferencesGoogleExtrasStore.
/// </summary>
internal sealed class InMemoryGoogleExtrasStore : IGoogleExtrasStore
{
    private bool _isAllowed = true;

    public bool Read() => _isAllowed;

    public void Write(bool isAllowed) => _isAllowed = isAllowed;
}
