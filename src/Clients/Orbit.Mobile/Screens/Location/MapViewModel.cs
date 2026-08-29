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

    /// <summary>Which of the two share buttons was pressed - see KeepSharingAsync.</summary>
    private bool _willKeepSharing;

    /// <summary>Refreshes every live share while this screen is open - see StartRefreshing.</summary>
    private CancellationTokenSource? _refreshing;

    /// <summary>
    /// How often a live share is sent again. The same minute Orbit.Web uses: a position a minute old is
    /// still where somebody is, and anything faster costs a phone battery for no one's benefit.
    /// </summary>
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(1);

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

    /// <summary>
    /// Shares where the reader is now, once. Whoever they pick sees the point they read a moment ago
    /// and nothing after it - which is the right answer for "here is where I am, come and find me".
    /// </summary>
    [RelayCommand]
    private Task ShareOnceAsync(CancellationToken cancellationToken)
        => StartSharingAsync(isContinuous: false, cancellationToken);

    /// <summary>
    /// Keeps sharing: the position goes out again every minute for as long as this screen is open.
    /// Orbit.Web offers both and the phone offered only the first, which is the wrong half to be
    /// missing - a phone is the thing that moves, and a browser mostly is not.
    /// </summary>
    [RelayCommand]
    private Task KeepSharingAsync(CancellationToken cancellationToken)
        => StartSharingAsync(isContinuous: true, cancellationToken);

    private async Task StartSharingAsync(bool isContinuous, CancellationToken cancellationToken)
    {
        Message = string.Empty;
        _willKeepSharing = isContinuous;
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
            Message = await _sharedLocations.ShareAsync(
                    contact.UserId, _ownPosition, _willKeepSharing, cancellationToken)
                ? _translations.Format(
                    _willKeepSharing
                        ? "Sharing with {0}. Your position goes out again every minute while this screen is open."
                        : "Shared with {0}.",
                    contact.DisplayName)
                : _translations.Format(
                    "{0} hasn't set up Orbit's encryption yet, so there is nothing to share to.",
                    contact.DisplayName);

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
            Message = _translations.Format("{0} can no longer see where you are.", row.DisplayName);
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


    /// <summary>
    /// Starts sending every live share again, once a minute, while this screen is open. Tied to the
    /// screen rather than running in the background, exactly as Orbit.Web ties it to the page: sharing
    /// where you are is something somebody is doing on purpose, and a loop that outlived the screen
    /// would keep broadcasting after they had put the phone down.
    ///
    /// A phone could do better than this with a foreground service, and one day should. What it must
    /// not do is quietly keep sending after the reader thinks they have stopped looking.
    /// </summary>
    public void StartRefreshing()
    {
        StopRefreshing();
        _refreshing = new CancellationTokenSource();
        _ = RefreshAsync(_refreshing.Token);
    }

    public void StopRefreshing()
    {
        _refreshing?.Cancel();
        _refreshing?.Dispose();
        _refreshing = null;
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(RefreshInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await SendLiveSharesAgainAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // The screen was left - the loop is meant to end with it.
        }
    }

    /// <summary>
    /// One round of the loop: read where the phone is now, and send it to everyone with a live share.
    /// Nothing is said on screen about it - a message appearing by itself every minute would read as
    /// something having happened, when what happened is what the reader already asked for.
    /// </summary>
    internal async Task SendLiveSharesAgainAsync(CancellationToken cancellationToken)
    {
        var live = SharingWith.Where(row => row.IsContinuous).ToList();
        if (live.Count == 0)
        {
            return;
        }

        try
        {
            var reading = await _deviceLocation.ReadAsync(cancellationToken);
            if (reading.Outcome is not DeviceLocationOutcome.Found)
            {
                // A reading that failed is a minute skipped, not a share ended: the next tick tries
                // again, and the last position anyone was sent is still the last one that was true.
                return;
            }

            var position = new SharedPosition(
                reading.Latitude, reading.Longitude, reading.Address, DateTimeOffset.UtcNow);
            _ownPosition = position;
            OwnPositionDescription = Describe(position);
            ShowPointsOnMap();

            await _locationClient.SaveOwnAsync(
                reading.Latitude, reading.Longitude, reading.Address, cancellationToken);
            foreach (var row in live)
            {
                await _sharedLocations.ShareAsync(row.UserId, position, isContinuous: true, cancellationToken);
            }

            await ShowWhoCanSeeMeAsync(cancellationToken);
        }
        catch (Exception exception)
            when (exception is HttpRequestException or OperationCanceledException or EncryptionKeyLockedException)
        {
            // Same as a failed reading: one minute missed. Sending the reader to the key gate from a
            // timer would take the screen away from under them without their having touched anything.
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
