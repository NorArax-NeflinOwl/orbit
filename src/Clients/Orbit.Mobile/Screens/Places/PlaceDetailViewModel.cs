using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Location;
using Orbit.Mobile.Sync;

namespace Orbit.Mobile.Screens.Places;

/// <summary>
/// One place: what it is called, what was written about it, where it is, how much it matters and what
/// colour its pin takes. The whole of a place, which is why there is no second screen behind this one.
///
/// Where it is can be typed or pointed at, the same pair the task editor offers and for the same reason:
/// pointing is what works when nobody knows what the street is called. A confirmed pin replaces the words
/// only when the box is empty, so "the back entrance" survives being pinned.
/// </summary>
public sealed partial class PlaceDetailViewModel : ObservableObject
{
    private readonly LocalPlaceRepository _places;
    private readonly PlaceSynchronizer _synchronizer;
    private readonly IPlacePicker _placePicker;
    private readonly IMapHandoff _maps;
    private readonly Translations _translations;
    private readonly INetworkStatus _networkStatus;
    private readonly IScreenNavigator _navigator;

    private Guid _localId;

    public PlaceDetailViewModel(
        LocalPlaceRepository places, PlaceSynchronizer synchronizer, IPlacePicker placePicker,
        IMapHandoff maps, Translations translations, INetworkStatus networkStatus, IScreenNavigator navigator)
    {
        _places = places;
        _synchronizer = synchronizer;
        _placePicker = placePicker;
        _maps = maps;
        _translations = translations;
        _networkStatus = networkStatus;
        _navigator = navigator;
        Priorities = Tasks.PriorityChoice.All(translations);
    }

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    /// <summary>Where it is, in words. Can be empty for a point nobody has named.</summary>
    [ObservableProperty]
    private string _address = string.Empty;

    [ObservableProperty]
    private string _colour = string.Empty;

    [ObservableProperty]
    private Tasks.PriorityChoice? _chosenPriority;

    [ObservableProperty]
    private string _message = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    /// <summary>
    /// Whether this reader may change it. False for a place handed over to read - the same refusal the
    /// server makes, said here so the screen does not offer a Save that would only fail.
    /// </summary>
    [ObservableProperty]
    private bool _canEdit = true;

    /// <summary>Why not, when they may not - see SharedItemAccess and OfflineEditPolicy.</summary>
    [ObservableProperty]
    private string _whyItIsReadOnly = string.Empty;

    /// <summary>Who handed it over, or empty for one this reader kept.</summary>
    [ObservableProperty]
    private string _sharedBy = string.Empty;

    private double _latitude;
    private double _longitude;

    /// <summary>How much it matters, as the picker offers it - see PriorityChoice.</summary>
    public IReadOnlyList<Tasks.PriorityChoice> Priorities { get; }

    public bool HasMessage => Message.Length > 0;

    partial void OnMessageChanged(string value) => OnPropertyChanged(nameof(HasMessage));

    public bool HasSharedBy => SharedBy.Length > 0;

    partial void OnSharedByChanged(string value) => OnPropertyChanged(nameof(HasSharedBy));

    public bool IsReadOnly => !CanEdit;

    partial void OnCanEditChanged(bool value) => OnPropertyChanged(nameof(IsReadOnly));

    /// <summary>
    /// Whether it has a point at all. A place without one cannot be drawn on a map and cannot be handed
    /// to one, which is why saving is refused until it has - the same pair the server checks.
    /// </summary>
    public bool HasAPoint => _latitude != 0 || _longitude != 0;

    /// <summary>The other half of it, so the screen can say so without a converter for "not".</summary>
    public bool NeedsAPoint => !HasAPoint;

    public bool CanSave => CanEdit && Name.Trim().Length > 0 && HasAPoint;

    partial void OnNameChanged(string value) => SaveCommand.NotifyCanExecuteChanged();

    public void Open(Guid localId)
    {
        _localId = localId;
        _ = LoadCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        if (await _places.FindAsync(_localId, cancellationToken) is not { } place)
        {
            Message = _translations["That place is not here any more."];
            return;
        }

        Name = place.Name;
        Description = place.Description;
        Address = place.Address;
        Colour = place.Colour;
        ChosenPriority = Tasks.PriorityChoice.For(place.Priority, _translations);
        _latitude = place.Latitude;
        _longitude = place.Longitude;
        SharedBy = place.IsShared ? place.SharedByUserName ?? string.Empty : string.Empty;

        CanEdit = SharedItemAccess.AllowsEditing(place) && OfflineEditPolicy.IsAllowed(place, _networkStatus);
        WhyItIsReadOnly = CanEdit
            ? string.Empty
            : SharedItemAccess.AllowsEditing(place)
                ? _translations[RefusalMessage]
                : SharedItemAccess.WhyItCannotBeEdited(place, _translations);

        SayWhetherItHasAPoint();
        SaveCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Points at it on a map instead of typing it - see IPlacePicker. The point is taken whatever the
    /// box says; the words only where there are none.
    /// </summary>
    [RelayCommand]
    private async Task PickOnMapAsync(CancellationToken cancellationToken)
    {
        if (!CanEdit)
        {
            return;
        }

        var picked = await _placePicker.PickAsync(Address, cancellationToken);
        if (picked.Outcome is not PickedPlaceOutcome.Chosen)
        {
            return;
        }

        _latitude = picked.Latitude ?? _latitude;
        _longitude = picked.Longitude ?? _longitude;

        if (Address.Trim().Length == 0)
        {
            Address = picked.Address;
        }

        if (Name.Trim().Length == 0)
        {
            Name = picked.Address;
        }

        SayWhetherItHasAPoint();
        SaveCommand.NotifyCanExecuteChanged();
    }

    private void SayWhetherItHasAPoint()
    {
        OnPropertyChanged(nameof(HasAPoint));
        OnPropertyChanged(nameof(NeedsAPoint));
    }

    /// <inheritdoc cref="PlacesViewModel.OpenInMapsAsync"/>
    [RelayCommand]
    private async Task OpenInMapsAsync(CancellationToken cancellationToken)
    {
        if (HasAPoint)
        {
            await _maps.ShowAsync(_latitude, _longitude, Name, cancellationToken);
        }
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        IsBusy = true;
        try
        {
            var outcome = await _places.UpdateAsync(
                _localId,
                new PlaceContent(
                    Name, Description, Address, _latitude, _longitude, Colour,
                    ChosenPriority?.Value ?? "Normal"),
                cancellationToken);

            if (outcome.WasRefused())
            {
                Message = outcome.Explain(RefusalMessage, _translations);
                return;
            }

            Message = string.Empty;
            await SynchroniseAsync(cancellationToken);
            _navigator.ShowPlaces();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SynchroniseAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _synchronizer.SynchroniseAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            // The change is written here and queued; the next run sends it. Nothing to say.
        }
    }

    /// <inheritdoc cref="PlacesViewModel.RefusalMessage"/>
    private const string RefusalMessage =
        "Somebody else can change this place, and Orbit can't be reached to check. It stays read-only until you're back online.";
}
