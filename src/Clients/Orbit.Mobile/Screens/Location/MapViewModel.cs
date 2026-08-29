using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Mobile.Api;
using Orbit.Mobile.Crypto;
using Orbit.Mobile.Data;
using Orbit.Mobile.Google;
using Orbit.Core.Permissions;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Permissions;
using Orbit.Mobile.Location;
using Orbit.Mobile.Sync;

namespace Orbit.Mobile.Screens.Location;

/// <summary>
/// Where the reader is, who they are letting see it, and where the people sharing with them are.
///
/// Nothing here is offline-capable, and that is the honest shape rather than a gap. A position is only
/// worth anything while it is current: showing yesterday's point on a map is worse than showing none,
/// because it looks exactly like today's. So this asks the device and the server each time and says so
/// when it cannot.
/// </summary>
public sealed partial class MapViewModel : ObservableObject
{
    private readonly IDeviceLocation _deviceLocation;
    private readonly LocationClient _locationClient;
    private readonly SharedLocations _sharedLocations;
    private readonly UsersClient _usersClient;
    private readonly ChatRepository _chatRepository;
    private readonly ChatSynchronizer _synchronizer;
    private readonly Translations _translations;
    private readonly UserPermissions _permissions;
    private readonly GoogleIntegrationAccess _google;
    private readonly IScreenNavigator _navigator;

    private SharedPosition? _ownPosition;

    [ObservableProperty]
    private string _ownPositionDescription = string.Empty;

    [ObservableProperty]
    private string _message = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isChoosingWhoToShareWith;

    /// <summary>
    /// Whether this account may hand a position off to Google - see GoogleIntegrationAccess for who
    /// qualifies. Read once the screen loads, so a "no" is not a flicker before the answer arrives.
    /// </summary>
    [ObservableProperty]
    private bool _hasGoogleExtras;

    public MapViewModel(
        IDeviceLocation deviceLocation, LocationClient locationClient, SharedLocations sharedLocations,
        UsersClient usersClient, ChatRepository chatRepository, ChatSynchronizer synchronizer,
        Translations translations, UserPermissions permissions, GoogleIntegrationAccess google,
        IScreenNavigator navigator)
    {
        _deviceLocation = deviceLocation;
        _locationClient = locationClient;
        _sharedLocations = sharedLocations;
        _usersClient = usersClient;
        _chatRepository = chatRepository;
        _synchronizer = synchronizer;
        _translations = translations;
        _permissions = permissions;
        _google = google;
        _navigator = navigator;
        OwnPositionDescription = _translations["Not read yet."];
    }

    /// <summary>True while this account cannot use the map at all - see LockedFeatureMessage.</summary>
    public bool IsLocked => !_permissions.Has(ApplicationPermission.Location);

    public bool IsUnlocked => !IsLocked;

    public string LockedExplanation => LockedFeatureMessage.For(ApplicationPermission.Location, _translations);

    [RelayCommand]
    private void OpenAccount() => _navigator.ShowAccount();

    /// <summary>People whose position the reader can currently see.</summary>
    public ObservableCollection<ReceivedPosition> SharedWithMe { get; } = [];

    /// <summary>
    /// Everything worth drawing: the reader's own position and everybody else's that could be opened.
    /// One collection rather than two, because a map does not care whose a point is - only the pin's
    /// label does.
    /// </summary>
    public ObservableCollection<MapPoint> Points { get; } = [];

    /// <summary>People who can currently see the reader's.</summary>
    public ObservableCollection<SharingWithRow> SharingWith { get; } = [];

    /// <summary>Who a position could be shared with: the people this phone has conversations with.</summary>
    public ObservableCollection<LocalContact> Candidates { get; } = [];

    public bool HasMessage => Message.Length > 0;

    public bool HasOwnPosition => _ownPosition is not null;

    /// <summary>
    /// The recorded position, opened in Google Maps. Null when there is nothing to point at - the URL is
    /// built here rather than in the page so it can be tested; opening it is the page's platform call.
    /// </summary>
    public string? OwnPositionInGoogleMapsUrl
        => _ownPosition is { } own ? GoogleMapsLink.ToPlace(own.Latitude, own.Longitude) : null;

    /// <summary>Both halves have to hold: something to point at, and an account allowed to point at it.</summary>
    public bool CanOpenOwnPositionInGoogleMaps => HasGoogleExtras && _ownPosition is not null;

    public bool IsNotChoosing => !IsChoosingWhoToShareWith;

    [RelayCommand]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        Message = string.Empty;
        if (IsLocked)
        {
            return;
        }

        IsBusy = true;
        try
        {
            HasGoogleExtras = await _google.IsAvailableAsync(cancellationToken);
            await ShowWhoIsSharingAsync(cancellationToken);
            await ShowWhoCanSeeMeAsync(cancellationToken);
        }
        catch (HttpRequestException)
        {
            // See ContactsViewModel: refused rather than unreachable, and it must not escape a command
            // nobody is awaiting.
            Message = _translations["Couldn't reach Orbit just now."];
        }
        catch (EncryptionKeyLockedException)
        {
            _navigator.ShowChatKeyGate();
        }
        catch (OperationCanceledException)
        {
            // The screen went away mid-load.
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Reads where the phone is and records it against the account. Recording and sharing are separate
    /// on purpose: knowing where you are is the reader's own business, and nobody else sees it until
    /// they say who.
    /// </summary>
    [RelayCommand]
    private async Task ReadMyPositionAsync(CancellationToken cancellationToken)
    {
        Message = string.Empty;
        IsBusy = true;
        try
        {
            var reading = await _deviceLocation.ReadAsync(cancellationToken);
            if (reading.Outcome is not DeviceLocationOutcome.Found)
            {
                Message = reading.Outcome is DeviceLocationOutcome.NotPermitted
                    ? _translations["Orbit needs permission to use your location. Turn it on in Settings."]
                    : _translations["Couldn't get a position - try again outdoors."];
                return;
            }

            _ownPosition = new SharedPosition(
                reading.Latitude, reading.Longitude, reading.Address, DateTimeOffset.UtcNow);
            OwnPositionDescription = Describe(_ownPosition);
            OnPropertyChanged(nameof(HasOwnPosition));
            OnPropertyChanged(nameof(OwnPositionInGoogleMapsUrl));
            OnPropertyChanged(nameof(CanOpenOwnPositionInGoogleMaps));
            ShowPointsOnMap();

            await _locationClient.SaveOwnAsync(
                reading.Latitude, reading.Longitude, reading.Address, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            // Reached-and-refused is not the same as unreachable, and telling somebody they are offline
            // when the server answered is unactionable - the same mistake the sync layer makes a point
            // of not making. A null status is the only thing that means the request never landed.
            Message = exception.StatusCode is null
                ? _translations["Read your position, but couldn't save it - Orbit is out of reach."]
                : _translations["Read your position, but Orbit wouldn't store it. Try signing in again."];
        }
        catch (OperationCanceledException)
        {
            // The screen went away mid-read.
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task StartSharingAsync(CancellationToken cancellationToken)
    {
        Message = string.Empty;
        if (_ownPosition is null)
        {
            Message = _translations["Read your position first."];
            return;
        }

        Candidates.Clear();
        foreach (var contact in await _chatRepository.GetContactsAsync(cancellationToken))
        {
            if (contact.PublicKeyBase64 is not null)
            {
                Candidates.Add(contact);
            }
        }

        if (Candidates.Count == 0)
        {
            // Sealing needs their key, and a key only exists once they have used Orbit.
            Message = _translations["Nobody to share with yet - start a conversation first."];
            return;
        }

        IsChoosingWhoToShareWith = true;
    }

    [RelayCommand]
    private void CancelSharing()
    {
        IsChoosingWhoToShareWith = false;
        Message = string.Empty;
    }

    [RelayCommand]
    private async Task ShareWithAsync(LocalContact? contact, CancellationToken cancellationToken)
    {
        if (contact is null || _ownPosition is null)
        {
            return;
        }

        IsChoosingWhoToShareWith = false;
        try
        {
            Message = await _sharedLocations.ShareAsync(contact.UserId, _ownPosition, isContinuous: false, cancellationToken)
                ? _translations.Format("Shared with {0}.", contact.DisplayName)
                : $"{contact.DisplayName} hasn't set up Orbit's encryption yet, so there is nothing to share to.";

            await ShowWhoCanSeeMeAsync(cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            Message = exception.StatusCode is null
                ? _translations["Sharing a position needs a connection."]
                : _translations["Orbit wouldn't accept that share. Try signing in again."];
        }
        catch (EncryptionKeyLockedException)
        {
            _navigator.ShowChatKeyGate();
        }
        catch (OperationCanceledException)
        {
            // The screen went away mid-share.
        }
    }

    [RelayCommand]
    private async Task StopSharingWithAsync(SharingWithRow? row, CancellationToken cancellationToken)
    {
        if (row is null)
        {
            return;
        }

        try
        {
            await _locationClient.StopSharingAsync(row.UserId, cancellationToken);
            Message = $"{row.DisplayName} can no longer see where you are.";
            await ShowWhoCanSeeMeAsync(cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            // Worth being precise about: whoever it is can still see the reader either way, and saying
            // "you are offline" when they are not sends them looking in the wrong place.
            Message = exception.StatusCode is null
                ? _translations["Stopping needs a connection - they can still see you until it goes through."]
                : _translations["Orbit wouldn't stop that share - they can still see you."];
        }
        catch (OperationCanceledException)
        {
            // The screen went away mid-stop.
        }
    }

    private async Task ShowWhoIsSharingAsync(CancellationToken cancellationToken)
    {
        var received = await _sharedLocations.ReadSharedWithMeAsync(cancellationToken);
        SharedWithMe.Clear();
        foreach (var position in received)
        {
            SharedWithMe.Add(position);
        }

        ShowPointsOnMap();
    }

    /// <summary>
    /// Rebuilds the whole set rather than adding and removing, because a position is replaced rather
    /// than edited - the same point moving is a new reading, and there is never enough here for the
    /// difference to be worth tracking.
    /// </summary>
    private void ShowPointsOnMap()
    {
        Points.Clear();

        if (_ownPosition is { } own)
        {
            Points.Add(new MapPoint(_translations["You"], own.Address, own.Latitude, own.Longitude, IsMine: true));
        }

        foreach (var received in SharedWithMe)
        {
            // A position that could not be opened has no coordinates to draw - it stays in the list
            // below, where it can at least say who is sharing.
            if (received.Position is { } position)
            {
                Points.Add(new MapPoint(
                    received.SharerDisplayName, position.Address, position.Latitude, position.Longitude, IsMine: false));
            }
        }
    }

    private async Task ShowWhoCanSeeMeAsync(CancellationToken cancellationToken)
    {
        // The contact cache names most of them; anybody it misses is looked up, because somebody can be
        // shared with without a conversation having happened since.
        await _synchronizer.SynchroniseContactsAsync(cancellationToken);
        var contacts = (await _chatRepository.GetContactsAsync(cancellationToken))
            .ToDictionary(contact => contact.UserId, contact => contact.DisplayName);

        var shares = await _locationClient.GetOwnSharesAsync(cancellationToken);
        SharingWith.Clear();
        foreach (var share in shares)
        {
            var displayName = contacts.GetValueOrDefault(share.RecipientUserId)
                ?? (await _usersClient.FindAsync(share.RecipientUserId, cancellationToken))?.DisplayName
                ?? _translations["Someone"];

            SharingWith.Add(SharingWithRow.From(
                share.RecipientUserId, displayName, share.IsContinuous, share.UpdatedAtUtc, _translations));
        }
    }

    private static string Describe(SharedPosition position)
        => position.Address is { Length: > 0 } address
            ? address
            : $"{position.Latitude:F5}, {position.Longitude:F5}";

    partial void OnMessageChanged(string value) => OnPropertyChanged(nameof(HasMessage));

    partial void OnIsChoosingWhoToShareWithChanged(bool value) => OnPropertyChanged(nameof(IsNotChoosing));

    /// <summary>The button appears the moment the answer arrives, not on the next reading.</summary>
    partial void OnHasGoogleExtrasChanged(bool value)
        => OnPropertyChanged(nameof(CanOpenOwnPositionInGoogleMaps));
}
