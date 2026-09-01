using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Contracts.Sharing;
using Orbit.Mobile.Api;
using Orbit.Mobile.Authentication;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Sync;

namespace Orbit.Mobile.Screens.Sharing;

/// <summary>One line of what a link shows, already worded - see PublicSharedItemLineDto.</summary>
/// <param name="IsTicked">Drawn as ticked, never tickable: this is somebody else's list being read.</param>
public sealed record SharedLine(string Text, string Detail, bool IsChecklistItem, bool IsTicked)
{
    public bool HasDetail => Detail.Length > 0;
}

/// <summary>
/// What somebody sees when they open a link they were sent - the phone's answer to Orbit.Web's /s/{token}
/// page, reached the same way: by following the link itself, which Android hands to the app rather than
/// the browser when Orbit is installed (see MainActivity's intent filter).
///
/// Read-only by nature. What comes back is a projection built by the server (PublicSharedItem), not the
/// item, so there is nothing here to edit and nothing of the owner's account to see beyond the name they
/// share things under. Taking a copy is the one action, and it needs an account, because a copy has to
/// belong to somebody.
/// </summary>
public sealed partial class SharedLinkViewModel : ObservableObject
{
    private readonly PublicShareClient _publicShares;
    private readonly SessionStore _sessionStore;
    private readonly INetworkStatus _networkStatus;
    private readonly Translations _translations;
    private readonly IScreenNavigator _navigator;

    private string _token = string.Empty;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _subtitle = string.Empty;

    [ObservableProperty]
    private string _kind = string.Empty;

    [ObservableProperty]
    private string _sharedBy = string.Empty;

    [ObservableProperty]
    private string _message = string.Empty;

    [ObservableProperty]
    private bool _isLoading = true;

    /// <summary>
    /// Whether anything could be shown at all. False covers every way a link fails, and the screen says
    /// one sentence for all of them - see PublicShareClient.ReadAsync.
    /// </summary>
    [ObservableProperty]
    private bool _wasFound;

    /// <summary>False for somebody not signed in, and once a copy has been taken.</summary>
    [ObservableProperty]
    private bool _canBeKept;

    public SharedLinkViewModel(
        PublicShareClient publicShares, SessionStore sessionStore, INetworkStatus networkStatus,
        Translations translations, IScreenNavigator navigator)
    {
        _publicShares = publicShares;
        _sessionStore = sessionStore;
        _networkStatus = networkStatus;
        _translations = translations;
        _navigator = navigator;
    }

    public ObservableCollection<SharedLine> Lines { get; } = [];

    public bool HasMessage => Message.Length > 0;

    public bool HasSubtitle => Subtitle.Length > 0;

    public bool HasNothingInIt => WasFound && Lines.Count == 0;

    partial void OnMessageChanged(string value) => OnPropertyChanged(nameof(HasMessage));

    partial void OnSubtitleChanged(string value) => OnPropertyChanged(nameof(HasSubtitle));

    partial void OnWasFoundChanged(bool value) => OnPropertyChanged(nameof(HasNothingInIt));

    /// <summary>The token out of the link that was followed - see NotificationDestination.</summary>
    public void Open(string token) => _token = token;

    [RelayCommand]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        IsLoading = true;
        try
        {
            var item = await _publicShares.ReadAsync(_token, cancellationToken);
            Show(item);

            // Asked after the link, not before: somebody with no account is meant to be able to read
            // this, and only the offer to keep it depends on being signed in.
            CanBeKept = item is not null && await _sessionStore.GetAsync() is not null;
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            Show(null);
            Message = _networkStatus.IsOnline
                ? _translations["This link couldn't be opened. Try again."]
                : _translations["A link can only be opened online. Try again when you are back."];
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Takes a read-only copy into this account. The copy appears once the feature's own synchroniser
    /// next runs, the same as accepting something offered in a conversation - see SharedItemAcceptance.
    /// </summary>
    [RelayCommand]
    private async Task KeepAsync(CancellationToken cancellationToken)
    {
        try
        {
            var claimed = await _publicShares.ClaimAsync(_token, cancellationToken);
            if (claimed is null)
            {
                Message = _translations["This couldn't be saved to your account. Try again."];
                return;
            }

            CanBeKept = false;
            Message = claimed.AlreadyHeld
                ? _translations["You already have this."]
                : _translations["Saved. It will appear once Orbit next syncs."];
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            Message = _translations["This couldn't be saved to your account. Try again."];
        }
    }

    [RelayCommand]
    private void GoBack() => _navigator.ShowDashboard();

    private void Show(PublicSharedItemDto? item)
    {
        Lines.Clear();
        WasFound = item is not null;

        if (item is null)
        {
            Title = _translations["This link doesn't work"];
            Subtitle = string.Empty;
            Kind = string.Empty;
            SharedBy = string.Empty;
            Message = _translations["It may have been turned off by whoever shared it, or the thing it pointed at may be gone. Ask them for a new one."];
            return;
        }

        Title = item.Title;
        Subtitle = item.Subtitle ?? string.Empty;
        Kind = NameOf(item.ItemType);
        SharedBy = _translations.Format("Shared by {0}", item.OwnerDisplayName);
        Message = string.Empty;

        foreach (var line in item.Lines)
        {
            Lines.Add(new SharedLine(line.Text, line.Detail ?? string.Empty, line.IsChecklistItem, line.IsChecked));
        }

        OnPropertyChanged(nameof(HasNothingInIt));
    }

    /// <summary>
    /// What kind of thing this is, in the reader's language. The server names it in the wire's own words
    /// - see CreatePublicShareLinkRequest.ItemType - which are not words to put on a screen.
    /// </summary>
    private string NameOf(string itemType) => itemType switch
    {
        "Note" => _translations["Note"],
        "TaskList" => _translations["Task list"],
        "CalendarEvent" => _translations["Event"],
        "Warehouse" => _translations["Warehouse"],
        // A kind added after this build. The rest of the screen reads perfectly well without a label.
        _ => string.Empty
    };
}
