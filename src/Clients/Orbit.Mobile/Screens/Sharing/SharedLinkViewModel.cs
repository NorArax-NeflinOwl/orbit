using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Contracts.Sharing;
using Orbit.Core.Notes;
using Orbit.Mobile.Api;
using Orbit.Mobile.Data;
using Orbit.Mobile.Authentication;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens.Notes;
using Orbit.Mobile.Sync;

namespace Orbit.Mobile.Screens.Sharing;

/// <summary>One line of what a link shows, already worded - see PublicSharedItemLineDto.</summary>
/// <param name="IsTicked">Drawn as ticked, never tickable: this is somebody else's list being read.</param>
/// <param name="Style">
/// What the line is, where the item is a note - a heading, a line of a list, ordinary writing. Only a
/// note sends one; everything else is a list of things and reads as Body.
/// </param>
/// <param name="ListNumber">
/// What a numbered line shows, worked out over all the lines at once when they arrive - see
/// <see cref="NoteLineLook.NumbersFor"/>.
/// </param>
/// <param name="TableRows">
/// The table this line is, where it is one - each row the words of its cells - and nothing for a line
/// of writing. See Orbit.Core.Notes.NoteTable; only a note ever sends one.
/// </param>
/// <param name="Marks">The marks on stretches of the words, drawn by MarkedLabel - see Orbit.Core.Notes.NoteTextRun.</param>
/// <param name="PictureId">Which picture this line is, where it is one - what its bytes are fetched by.</param>
/// <param name="PictureBytes">The picture's bytes once fetched, null until then - see NotePictureCache.</param>
/// <param name="PictureNote">What stands in the picture's place while there are no bytes: that it is on its way, or why it is not.</param>
public sealed record SharedLine(
    string Text, string Detail, bool IsChecklistItem, bool IsTicked,
    NoteLineStyle Style = NoteLineStyle.Body, int ListNumber = 0,
    IReadOnlyList<IReadOnlyList<string>>? TableRows = null, bool IsAPicture = false,
    IReadOnlyList<NoteTextRun>? Marks = null, Guid? PictureId = null, byte[]? PictureBytes = null,
    string PictureNote = "")
{
    public bool HasDetail => Detail.Length > 0;

    /// <summary>The picture drawn, once fetched - a line is replaced by one carrying the bytes, see SharedLinkViewModel.ShowThePicturesAsync.</summary>
    public bool ShowsPicture => IsAPicture && PictureBytes is not null;

    /// <summary>What stands in the picture's place until it is fetched, or when it cannot be.</summary>
    public bool ShowsPictureNote => IsAPicture && PictureBytes is null;

    /// <summary>The marks as something to bind without a null check - see <see cref="Marks"/>.</summary>
    public IReadOnlyList<NoteTextRun> AllMarks => Marks ?? NoteTextMarks.None;

    /// <summary>Whether this line is a table, which shows the grid instead of the words.</summary>
    public bool IsATable => TableRows is { Count: > 0 };

    /// <summary>The words drawn as words - nothing for a table, whose words are in its cells, and nothing for a picture.</summary>
    public bool ShowsWords => !IsATable && !IsAPicture;

    /// <summary>How large the line is drawn - the same rules the note's own screen draws by.</summary>
    public double DrawnFontSize => NoteLineLook.SizeOf(Style);

    /// <inheritdoc cref="NoteLineLook.IsBold"/>
    public bool IsDrawnBold => NoteLineLook.IsBold(Style);

    /// <inheritdoc cref="NoteLineLook.MarkOf"/>
    public string ListMark => NoteLineLook.MarkOf(Style, ListNumber);

    /// <summary>
    /// Whether that mark is shown. Not on a line that also has a box: the box is already the mark at the
    /// head of the line - the rule NoteLineRow has, and the browser's stylesheet with it.
    /// </summary>
    public bool ShowsListMark => ListMark.Length > 0 && !IsChecklistItem;
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
        Translations translations, IScreenNavigator navigator, NotePictureCache? pictures = null)
    {
        _publicShares = publicShares;
        _sessionStore = sessionStore;
        _networkStatus = networkStatus;
        _translations = translations;
        _navigator = navigator;
        _pictures = pictures;
    }

    /// <summary>Where a picture's bytes come from - see NotePictureCache. Null on a screen built without one, which draws no pictures.</summary>
    private readonly NotePictureCache? _pictures;

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
            await ShowThePicturesAsync(cancellationToken);

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

        // The numbers first, over all the lines at once: a numbered line's number is its place in the
        // run above it, which no single line knows.
        var styles = item.Lines.Select(line => NoteLineStyles.Read(line.Style)).ToList();
        var numbers = NoteLineLook.NumbersFor(styles);

        for (var index = 0; index < item.Lines.Count; index++)
        {
            var line = item.Lines[index];
            Lines.Add(new SharedLine(
                line.Text, line.Detail ?? string.Empty, line.IsChecklistItem, line.IsChecked,
                styles[index], numbers[index],
                line.Table?.Rows.Select(row => (IReadOnlyList<string>)[.. row.Cells.Select(cell => cell.Text)]).ToList(),
                line.Picture is not null,
                NoteTextMarks.Normalized(
                    line.AllMarks.Select(run => new NoteTextRun(run.Start, run.Length, NoteTextMarks.Read(run.Mark))),
                    line.Text.Length),
                line.Picture?.PictureId,
                PictureNote: _translations["Picture"]));
        }

        OnPropertyChanged(nameof(HasNothingInIt));
    }

    /// <summary>
    /// Fetches each picture after the lines are shown, so the words never wait for the bytes, and
    /// puts a line carrying them in the place of the one that named it - the lines are records, so a
    /// fetched picture is a new line rather than a changed one. A picture through a link is never
    /// sealed, so the one reason it cannot be shown is that it could not be fetched.
    /// </summary>
    private async Task ShowThePicturesAsync(CancellationToken cancellationToken)
    {
        if (_pictures is null)
        {
            return;
        }

        for (var index = 0; index < Lines.Count; index++)
        {
            if (Lines[index].PictureId is not { } pictureId)
            {
                continue;
            }

            var bytes = await _pictures.OpenSharedAsync(_token, pictureId, cancellationToken);
            Lines[index] = bytes is null
                ? Lines[index] with { PictureNote = _translations["This picture couldn't be fetched. Try again when you are back online."] }
                : Lines[index] with { PictureBytes = bytes };
        }
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
        "Inventory" => _translations["Inventory"],
        // A kind added after this build. The rest of the screen reads perfectly well without a label.
        _ => string.Empty
    };
}
