using CommunityToolkit.Mvvm.ComponentModel;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Sync;

namespace Orbit.Mobile.Screens.Navigation;

/// <summary>
/// The line in the bottom-left corner saying whether the app is in step with the server.
///
/// Its own view model rather than a property on each screen's: the strip is on every page, and the
/// answer is about the app rather than about the page - see <see cref="SyncState"/>.
/// </summary>
public sealed partial class StatusStripViewModel : ObservableObject, IDisposable
{
    private readonly SyncState _syncState;
    private readonly Translations _translations;

    [ObservableProperty]
    private string _label = string.Empty;

    [ObservableProperty]
    private bool _isSyncing;

    /// <summary>True for the one condition worth a second look, so the strip can colour it.</summary>
    [ObservableProperty]
    private bool _needsAttention;

    public StatusStripViewModel(SyncState syncState, Translations translations)
    {
        _syncState = syncState;
        _translations = translations;
        _syncState.Changed += OnSyncStateChanged;
        Show();
    }

    public void Dispose() => _syncState.Changed -= OnSyncStateChanged;

    private void OnSyncStateChanged(object? sender, EventArgs e) => Show();

    private void Show()
    {
        Label = _syncState.Condition switch
        {
            SyncCondition.Syncing => _translations["Syncing…"],
            SyncCondition.Synced => _translations["Synced"],
            SyncCondition.Offline => _translations["Offline"],
            SyncCondition.Failed => _translations["Couldn't sync"],
            // Before anything has tried, saying "Synced" would be a claim and saying "Offline" a
            // slander. The strip stays quiet instead.
            _ => string.Empty
        };

        IsSyncing = _syncState.Condition == SyncCondition.Syncing;
        NeedsAttention = _syncState.Condition == SyncCondition.Failed;
    }
}
