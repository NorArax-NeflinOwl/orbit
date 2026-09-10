using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Location;
using Orbit.Mobile.Sync;

namespace Orbit.Mobile.Screens.Places;

/// <summary>
/// The places screen: somewhere on the map worth keeping, and the ones somebody handed over. Reads the
/// local database and never the API - that is what makes it work with no connection, and it is
/// structural rather than an optimisation (info/orbit-maui-plan.md §6).
///
/// The phone has no map of its own to draw these on, and this is deliberately not one: a place is a name,
/// a point and three answers about how it is drawn, and a list of names is the half a phone screen is
/// good at. Getting to the point itself is the device's job - see OpenInMapsAsync, which hands it to
/// whatever the reader uses for directions.
/// </summary>
public sealed partial class PlacesViewModel : ObservableObject
{
    private readonly LocalPlaceRepository _places;
    private readonly PlaceSynchronizer _synchronizer;
    private readonly INetworkStatus _networkStatus;
    private readonly Translations _translations;
    private readonly SyncState _syncState;
    private readonly IScreenNavigator _navigator;
    private readonly IMapHandoff _maps;
    private readonly IListArrangementStore _arrangements;
    private readonly TimeProvider _clock;

    [ObservableProperty]
    private string _newPlaceName = string.Empty;

    [ObservableProperty]
    private bool _isRefreshing;

    [ObservableProperty]
    private string _message = string.Empty;

    public PlacesViewModel(
        LocalPlaceRepository places, PlaceSynchronizer synchronizer, INetworkStatus networkStatus,
        Translations translations, SyncState syncState, IScreenNavigator navigator, IMapHandoff maps,
        TimeProvider clock, IListArrangementStore arrangements)
    {
        _places = places;
        _synchronizer = synchronizer;
        _networkStatus = networkStatus;
        _translations = translations;
        _syncState = syncState;
        _navigator = navigator;
        _maps = maps;
        _clock = clock;
        _arrangements = arrangements;
        _arrangement = arrangements.Read(ListSection.Places);
        Places.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasPlaces));
    }

    public ObservableCollection<PlaceListItem> Places { get; } = [];

    /// <inheritdoc cref="Notes.NotesViewModel.HasNotes"/>
    public bool HasPlaces => Places.Count > 0;

    /// <inheritdoc cref="Tasks.TasksViewModel.HasMessage"/>
    public bool HasMessage => Message.Length > 0;

    partial void OnMessageChanged(string value) => OnPropertyChanged(nameof(HasMessage));

    /// <inheritdoc cref="Notes.NotesViewModel.LoadAsync"/>
    [RelayCommand]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        await ShowLocalPlacesAsync(cancellationToken);
        await SynchroniseAsync(cancellationToken);
    }

    [RelayCommand]
    private void Open(PlaceListItem? row)
    {
        if (row is not null)
        {
            _navigator.ShowPlace(row.LocalId);
        }
    }

    /// <summary>
    /// Hands the point to whatever this phone uses for directions. The one thing a list of places is
    /// actually for and the one thing this app should not try to do itself - see IMapHandoff.
    /// </summary>
    [RelayCommand]
    private async Task OpenInMapsAsync(PlaceListItem? row, CancellationToken cancellationToken)
    {
        if (row is null || await _places.FindAsync(row.LocalId, cancellationToken) is not { } place)
        {
            return;
        }

        await _maps.ShowAsync(place.Latitude, place.Longitude, place.Name, cancellationToken);
    }

    /// <summary>
    /// A name and nothing else, which is all this box asks for: where it is comes next, on the place's
    /// own screen, because it needs a map or an address and neither fits on a row.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanAddPlace))]
    private async Task AddPlaceAsync(CancellationToken cancellationToken)
    {
        var created = await _places.CreateAsync(
            new PlaceContent(NewPlaceName.Trim(), string.Empty, string.Empty, 0, 0), cancellationToken);
        NewPlaceName = string.Empty;

        await ShowLocalPlacesAsync(cancellationToken);
        // Straight into it: a place with no point is not finished, and the screen that asks for one is
        // the place's own.
        _navigator.ShowPlace(created.LocalId);
    }

    private bool CanAddPlace => NewPlaceName.Trim().Length > 0;

    partial void OnNewPlaceNameChanged(string value) => AddPlaceCommand.NotifyCanExecuteChanged();

    /// <inheritdoc cref="Notes.NotesViewModel.DeleteAsync"/>
    [RelayCommand]
    private async Task DeleteAsync(PlaceListItem? row, CancellationToken cancellationToken)
    {
        if (row is null)
        {
            return;
        }

        var deletion = await _places.DeleteAsync(row.LocalId, cancellationToken);
        if (deletion.WasRefused())
        {
            Message = deletion.Explain(RefusalMessage, _translations);
            return;
        }

        Message = string.Empty;
        await ShowLocalPlacesAsync(cancellationToken);
        await SynchroniseAsync(cancellationToken);
    }

    /// <inheritdoc cref="Notes.NotesViewModel.Arrangement"/>
    [ObservableProperty]
    private ListArrangement _arrangement;

    [RelayCommand]
    private async Task ArrangeAsync(ListArrangement? arrangement, CancellationToken cancellationToken)
    {
        if (arrangement is null || arrangement == Arrangement)
        {
            return;
        }

        Arrangement = arrangement;
        _arrangements.Write(ListSection.Places, arrangement);
        await ShowLocalPlacesAsync(cancellationToken);
    }

    /// <summary>What the shared ordering needs to know about one row - see ListArrangements.</summary>
    private static ListRowFacts Describe(PlaceListItem row)
        => new(row.Name, row.UpdatedAtUtc, IsPinned: false, row.PriorityValue);

    private async Task ShowLocalPlacesAsync(CancellationToken cancellationToken)
    {
        var stored = await _places.GetAllAsync(cancellationToken);
        var pending = await _places.GetPendingLocalIdsAsync(cancellationToken);

        var rows = stored.Select(place => PlaceListItem.From(
            place, pending.Contains(place.LocalId), _networkStatus, _translations, _clock.GetUtcNow()));

        Places.Clear();
        foreach (var row in ListArrangements.Apply(rows, Arrangement, Describe))
        {
            Places.Add(row);
        }
    }

    /// <inheritdoc cref="Notes.NotesViewModel.SynchroniseAsync"/>
    private async Task SynchroniseAsync(CancellationToken cancellationToken)
    {
        IsRefreshing = true;
        _syncState.RecordStarted();
        try
        {
            var result = await _synchronizer.SynchroniseAsync(cancellationToken);
            if (result.ReachedTheServer)
            {
                _syncState.RecordSucceeded();
            }
            else
            {
                _syncState.RecordFailed();
            }

            if (result.Sent + result.Received + result.RemovedLocally > 0)
            {
                await ShowLocalPlacesAsync(cancellationToken);
            }
        }
        catch (HttpRequestException)
        {
            _syncState.RecordFailed();
        }
        catch (OperationCanceledException)
        {
            // The screen went away mid-sync. The command is started without being awaited, so this
            // must not escape.
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    /// <inheritdoc cref="Notes.NotesViewModel.RefusalMessage"/>
    private const string RefusalMessage =
        "Somebody else can change this place, and Orbit can't be reached to check. It stays read-only until you're back online.";
}
