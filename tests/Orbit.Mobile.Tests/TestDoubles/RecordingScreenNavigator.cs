using Orbit.Mobile.Data;
using Orbit.Mobile.Screens;

namespace Orbit.Mobile.Tests.TestDoubles;

/// <summary>
/// Remembers where a view model tried to send the reader, instead of swapping a window's page. The only
/// thing standing between the screens and an ordinary test project - see <see cref="IScreenNavigator"/>.
/// </summary>
internal sealed class RecordingScreenNavigator : IScreenNavigator
{
    private readonly List<string> _destinations = [];

    /// <summary>Where it was sent, in order, named after the method that sent it.</summary>
    public IReadOnlyList<string> Destinations => _destinations;

    public string? LastDestination => _destinations.Count == 0 ? null : _destinations[^1];

    public void ShowSignIn() => _destinations.Add(nameof(ShowSignIn));

    public void ShowRegister() => _destinations.Add(nameof(ShowRegister));

    public void ShowAccount() => _destinations.Add(nameof(ShowAccount));

    public void ShowChatKeyGate() => _destinations.Add(nameof(ShowChatKeyGate));

    public void ShowContacts() => _destinations.Add(nameof(ShowContacts));

    public void ShowConversation(LocalContact contact) => _destinations.Add(nameof(ShowConversation));

    public void ShowGroups() => _destinations.Add(nameof(ShowGroups));

    public void ShowGroupConversation(LocalChatGroup group) => _destinations.Add(nameof(ShowGroupConversation));

    public void ShowNotes() => _destinations.Add(nameof(ShowNotes));

    public void ShowTasks() => _destinations.Add(nameof(ShowTasks));

    public void ShowTaskList(Guid localId) => _destinations.Add(nameof(ShowTaskList));

    public void ShowCalendar() => _destinations.Add(nameof(ShowCalendar));

    public void ShowInventory() => _destinations.Add(nameof(ShowInventory));

    public void ShowWarehouse(Guid localId) => _destinations.Add(nameof(ShowWarehouse));
}
