using Orbit.Mobile.Screens.Chat;

namespace Orbit.Mobile.Tests.TestDoubles;

/// <summary>
/// The pinned conversations, held in memory rather than in the device's preferences - what a test uses
/// where the app uses PreferencesConversationPinStore. Kept across reads, so a test can check that
/// pinning something survives the list being read again.
/// </summary>
internal sealed class InMemoryConversationPinStore : IConversationPinStore
{
    private IReadOnlySet<Guid> _pinned = new HashSet<Guid>();

    public IReadOnlySet<Guid> Read() => _pinned;

    public void Write(IReadOnlySet<Guid> pinned) => _pinned = new HashSet<Guid>(pinned);
}
