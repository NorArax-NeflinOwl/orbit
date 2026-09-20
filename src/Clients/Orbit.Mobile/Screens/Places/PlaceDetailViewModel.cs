using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Chat;
using Orbit.Mobile.Location;
using Orbit.Mobile.Screens.Sharing;
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
        IMapHandoff maps, Translations translations, INetworkStatus networkStatus, IScreenNavigator navigator,
        SharePanel share, IDeviceLocation deviceLocation, PlaceSearch placeSearch)
    {
        _places = places;
        _synchronizer = synchronizer;
        _placePicker = placePicker;
        _maps = maps;
        _translations = translations;
        _networkStatus = networkStatus;
        _navigator = navigator;
        _deviceLocation = deviceLocation;
        _placeSearch = placeSearch;
        Share = share;
        Priorities = Tasks.PriorityChoice.All(translations);
    }

    /// <summary>Where this phone is, for "I am here" - the same reader the event's form uses.</summary>
    private readonly IDeviceLocation _deviceLocation;

    /// <summary>What a typed address is - see <see cref="PlaceSearch"/>, and SaveAsync, which asks it.</summary>
    private readonly PlaceSearch _placeSearch;

    /// <summary>Offering this place to somebody else - see SharePanel, which every editor here holds.</summary>
    public SharePanel Share { get; }

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

    /// <summary>
    /// Whether the place is sealed - encrypted on this phone, so Orbit holds no readable copy of where
    /// it is (see Orbit.Core.Places.Place.IsPrivate). A place is sealed unless its owner says otherwise,
    /// which is the opposite default from everything else in Orbit and deliberate: where somebody
    /// actually goes is the most personal thing the app holds.
    ///
    /// The phone had no control for it at all and no field on the save, so every place made or edited
    /// here was sealed and stayed sealed - and since a sealed place cannot be shared (the server
    /// refuses; see SharePlaceCommandHandler), pressing Share on one and choosing somebody answered
    /// "Couldn't share that." for a place whose owner had never chosen to seal it. Reported
    /// 2026-09-20 as "sharing doesn't work".
    /// </summary>
    [ObservableProperty]
    private bool _isSealed = true;

    /// <summary>
    /// Said in place of the sharing panel while the place is sealed: there is nothing to hand anybody,
    /// and the reason is one press away rather than a mystery.
    /// </summary>
    public string WhyItCannotBeShared
        => IsSealed ? _translations["Sealed places can't be shared. Take the seal off to offer this to somebody."] : string.Empty;

    public bool HasWhyItCannotBeShared => WhyItCannotBeShared.Length > 0;

    private double _latitude;
    private double _longitude;

    /// <summary>
    /// The lists this place belongs to, as it was loaded - see LocalPlace.TaskListIds. Kept and handed
    /// back on every save because a save writes the whole place: nothing on this screen shows or changes
    /// them, and passing nothing emptied them, so editing a place on the phone quietly unlinked it from
    /// every list the browser had put it on.
    /// </summary>
    private IReadOnlyList<Guid> _taskListIds = [];

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

    /// <summary>
    /// The other half of it, so the screen can say so without a converter for "not" - and only while
    /// there is no address either, since an address is a point this screen has not looked up yet.
    /// </summary>
    public bool NeedsAPoint => !HasAPoint && Address.Trim().Length == 0;

    /// <summary>
    /// A typed address counts: the save looks it up (see <see cref="TryFindTheTypedAddressAsync"/>) and
    /// refuses with a reason if nothing is found. It used to insist on a point, which no amount of
    /// typing could give - so the only way to keep a place at all was the map, and Save sat greyed with
    /// nothing saying what it wanted (reported 2026-09-20).
    /// </summary>
    public bool CanSave => CanEdit && Name.Trim().Length > 0 && (HasAPoint || Address.Trim().Length > 0);

    partial void OnNameChanged(string value) => SaveCommand.NotifyCanExecuteChanged();

    partial void OnAddressChanged(string value)
    {
        SaveCommand.NotifyCanExecuteChanged();
        SayWhetherItHasAPoint();
    }

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
        IsSharedWithMe = place.IsShared;
        IsArchived = place.IsArchived;

        IsSealed = place.IsPrivate;
        _taskListIds = place.TaskListIds;

        // Only a place the server knows about can be offered: a share names it by its server id, and
        // one still waiting in the outbox has none. And only an unsealed one: a sealed place has no
        // readable copy on the server to hand anybody, which is what makes it sealed - the same guard
        // the note, task list and inventory screens have had, and the one this screen was missing.
        // Since a place is sealed unless its owner says otherwise, the ordinary case was the refused
        // one: pressing Share and choosing somebody answered "Couldn't share that."
        if (place is { ServerId: { } serverId, IsPrivate: false })
        {
            Share.Describes(SharedItemKind.Place, serverId, place.Name, OwnerToAsk(place));
        }
        else
        {
            Share.OffersNothing();
        }

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

        // The name is left for the reader to give, and Save waits for one. It used to be filled with the
        // pin's address, so a place worth keeping was saved under a street it already showed beside it -
        // which is exactly the name the user asked not to be written in for them.

        SayWhetherItHasAPoint();
        SaveCommand.NotifyCanExecuteChanged();
    }

    private void SayWhetherItHasAPoint()
    {
        OnPropertyChanged(nameof(HasAPoint));
        OnPropertyChanged(nameof(NeedsAPoint));
    }

    /// <summary>
    /// Turns an address somebody typed into a point, where the place has none yet - the same lookup the
    /// event's form makes of the place typed into it (see PlaceSearch). Leaves a place that already has
    /// a point alone: the map put it there, and words typed beside it are a name for it rather than a
    /// correction of it.
    /// </summary>
    private async Task TryFindTheTypedAddressAsync(CancellationToken cancellationToken)
    {
        if (HasAPoint || Address.Trim() is not { Length: > 0 } typed)
        {
            return;
        }

        try
        {
            if (await _placeSearch.SearchAsync(typed, limit: 1, cancellationToken) is [var found, ..])
            {
                _latitude = found.Latitude;
                _longitude = found.Longitude;
                SayWhetherItHasAPoint();
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            // Nothing to look it up with. The caller says so: it is about to refuse the save anyway.
        }
    }

    /// <summary>
    /// Where this phone is, as the map's own screen offers it. The place screen had only the map
    /// picker, so keeping the place somebody is standing in meant finding it on a map first - asked for
    /// on 2026-09-20. The address it comes back with fills the box only while the box is empty:
    /// whatever somebody typed is worth more than a reverse-geocoded street.
    /// </summary>
    [RelayCommand]
    private async Task UseMyLocationAsync(CancellationToken cancellationToken)
    {
        if (!CanEdit)
        {
            return;
        }

        var here = await _deviceLocation.ReadAsync(cancellationToken);
        if (here.Outcome is not DeviceLocationOutcome.Found)
        {
            // The two refusals read the same to somebody standing here: no place was recorded.
            Message = _translations["Couldn't work out where this phone is."];
            return;
        }

        _latitude = here.Latitude;
        _longitude = here.Longitude;
        if (Address.Trim().Length == 0 && here.Address is { Length: > 0 } address)
        {
            Address = address;
        }

        Message = string.Empty;
        SayWhetherItHasAPoint();
        SaveCommand.NotifyCanExecuteChanged();
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
            // An address somebody typed is looked up before the place is written, the way the event's
            // form looks up the place typed into it. Without this the only way to give a place a point
            // was the map - typing "Rynek 1" and pressing Save was refused, with the field saying
            // nothing about why (reported 2026-09-20).
            await TryFindTheTypedAddressAsync(cancellationToken);
            if (!HasAPoint)
            {
                // A lookup that found nothing leaves the words alone and says so: a place with no point
                // cannot be drawn on a map and the server refuses it, so this is the refusal said here
                // rather than after a round trip.
                Message = _translations["Couldn't find that address. Pick it on the map instead."];
                return;
            }

            var outcome = await _places.UpdateAsync(
                _localId,
                new PlaceContent(
                    Name, Description, Address, _latitude, _longitude, Colour,
                    ChosenPriority?.Value ?? "Normal",
                    // Both as the place actually is, rather than as this record defaults. Sealed is the
                    // default, so every save from this phone re-sealed a place its owner had opened and
                    // emptied its readable fields again; and no lists at all is the default, so a save
                    // unlinked the place from every list the browser had put it on.
                    TaskListIds: _taskListIds,
                    IsPrivate: IsSealed),
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

    /// <summary>
    /// Whether this arrived through somebody else's share, which decides what deleting it is called and
    /// what it does - see LocalPlaceRepository.DeleteAsync.
    /// </summary>
    [ObservableProperty]
    private bool _isSharedWithMe;

    /// <summary>
    /// Whether it has been put away - what the screen's menu reads to say Archive or Put back, and what
    /// decides whether Delete is offered at all: deleting a place is offered in the archive and nowhere
    /// else (the user's rule, 2026-09-19, which Orbit.Web follows too).
    /// </summary>
    [ObservableProperty]
    private bool _isArchived;

    /// <summary>
    /// Puts it away, or brings it back - its own kind of change, queued as such, see
    /// LocalPlaceRepository.ArchiveAsync. Mirrors NoteDetailViewModel.ArchiveAsync, including saying so:
    /// nothing else on this screen moves, and a press that changes nothing visible reads as one that
    /// did nothing.
    /// </summary>
    [RelayCommand]
    private async Task ArchiveAsync(bool isArchived, CancellationToken cancellationToken)
    {
        var outcome = await _places.ArchiveAsync(_localId, isArchived, cancellationToken);
        if (outcome.WasRefused())
        {
            Message = outcome.Explain(RefusalMessage, _translations);
            return;
        }

        IsArchived = isArchived;
        Message = isArchived
            ? _translations["Archived - it is in the archive now."]
            : _translations["Put back where it was."];
        await SynchroniseAsync(cancellationToken);
    }

    /// <inheritdoc cref="PlacesViewModel.DeleteAsync"/>
    [RelayCommand]
    private async Task DeleteAsync(CancellationToken cancellationToken)
    {
        var deletion = await _places.DeleteAsync(_localId, cancellationToken);
        if (deletion.WasRefused())
        {
            Message = deletion.Explain(RefusalMessage, _translations);
            return;
        }

        await SynchroniseAsync(cancellationToken);
        _navigator.ShowPlaces();
    }

    /// <inheritdoc cref="Notes.NoteDetailViewModel.OwnerToAsk"/>
    private static Guid? OwnerToAsk(LocalPlace place)
        => place.AccessLevel == "CanEdit" ? null : place.OwnerUserId;

    /// <inheritdoc cref="PlacesViewModel.RefusalMessage"/>
    private const string RefusalMessage =
        "Somebody else can change this place, and Orbit can't be reached to check. It stays read-only until you're back online.";
}
