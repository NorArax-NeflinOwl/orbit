using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Mobile.Api;
using Orbit.Localization;
using Orbit.Mobile.Authentication;
using Orbit.Mobile.Data;
using Orbit.Core;
using Orbit.Mobile.Localization;
using Orbit.Core.Permissions;
using Orbit.Mobile.Permissions;
using Orbit.Mobile.Presence;
using Orbit.Mobile.Sync;
using Orbit.Mobile.Update;

namespace Orbit.Mobile.Screens.Navigation;

/// <summary>
/// The bar across the top of every signed-in screen: the way to each section, and who is signed in.
///
/// Mirrors what Orbit.Web shows on a narrow window, where the sidebar becomes exactly this - a logo
/// standing in for the Dashboard link, the section icons without their labels, and the avatar pushed to
/// the far right (see app.css's 680px breakpoint). Matching it is the point: somebody who uses both
/// should not have to learn the app twice.
///
/// One shared instance rather than one per page: the bar and the menu the avatar opens are two
/// controls that have to agree about whether that menu is open, and only one page is ever on screen.
/// </summary>
public sealed partial class NavigationBarViewModel : ObservableObject
{
    private readonly SessionStore _sessionStore;
    private readonly NotificationsClient _notificationsClient;
    private readonly AuthenticationClient _authenticationClient;
    private readonly Presence.Presence _presence;
    private readonly Translations _translations;
    private readonly LocalStoreReset _localStore;
    private readonly UserPermissions _permissions;
    private readonly SyncState _syncState;
    private readonly EverythingSynchronizer _synchronizer;
    private readonly INetworkStatus _networkStatus;
    private readonly MobileVersionGate _versionGate;
    private readonly IScreenNavigator _navigator;

    /// <summary>The signed-in reader's initials, which is what the avatar shows - there are no pictures in Orbit.</summary>
    [ObservableProperty]
    private string _initials = string.Empty;

    [ObservableProperty]
    private string _unreadLabel = string.Empty;

    [ObservableProperty]
    private PresenceAppearance _appearance = PresenceAppearance.Active;

    /// <summary>Whose account this is, shown at the top of the menu as the web's dropdown does.</summary>
    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private bool _isMenuOpen;

    /// <summary>
    /// Whether the status choices are showing. Collapsed by default: the menu's job is to list what is
    /// behind the avatar, and a row that is already unfolded pushes the rest of the list down to offer
    /// something most visits do not want.
    /// </summary>
    [ObservableProperty]
    private bool _isStatusExpanded;

    /// <summary>Whether the language choices are showing, the same folded row the status uses.</summary>
    [ObservableProperty]
    private bool _isLanguageExpanded;

    /// <summary>
    /// Whether the app is in step with the server, said beside the name in the avatar's menu.
    ///
    /// It used to sit in the bottom-left corner of all twenty screens, which is where the web puts it
    /// and where a phone has least room. It is a fact about the account rather than about whatever is
    /// on screen, so it belongs with the account.
    /// </summary>
    [ObservableProperty]
    private string _syncLabel = string.Empty;

    [ObservableProperty]
    private bool _isSyncing;

    /// <summary>Whether the map belongs on the bar at all - see ShowPermissions.</summary>
    [ObservableProperty]
    private bool _canUseLocation = true;

    /// <summary>
    /// Whether the conversations icon belongs on the bar. Either kind of conversation is enough: the
    /// screen behind it lists both, and hiding it for somebody who has groups but not one-to-one chat
    /// would put their groups out of reach.
    /// </summary>
    [ObservableProperty]
    private bool _canUseConversations = true;

    /// <summary>
    /// The four repositories, as the two copy windows know them - see Data.ICopyReviewStore. The bar
    /// asks them how much is outstanding, which is what puts the review within reach from any screen:
    /// a copy can be of any of the four kinds, so no one list is the right place to wait for it.
    /// </summary>
    private readonly IReadOnlyList<Data.ICopyReviewStore> _copyStores;

    public NavigationBarViewModel(
        SessionStore sessionStore, NotificationsClient notificationsClient,
        AuthenticationClient authenticationClient, Presence.Presence presence, Translations translations,
        LocalStoreReset localStore, UserPermissions permissions, SyncState syncState,
        MobileVersionGate versionGate, ServerVersionClient serverVersion, IScreenNavigator navigator,
        EverythingSynchronizer synchronizer, INetworkStatus networkStatus,
        IEnumerable<Data.ICopyReviewStore> copyStores)
    {
        _copyStores = [.. copyStores];
        _serverVersion = serverVersion;
        _sessionStore = sessionStore;
        _notificationsClient = notificationsClient;
        _authenticationClient = authenticationClient;
        _presence = presence;
        _translations = translations;
        _localStore = localStore;
        _permissions = permissions;
        _syncState = syncState;
        _synchronizer = synchronizer;
        _networkStatus = networkStatus;
        _networkStatus.Changed += (_, _) => ShowWhetherToOfferReconnecting();
        _versionGate = versionGate;

        _navigator = navigator;
        _presence.Changed += OnPresenceChanged;
        _permissions.Changed += OnPermissionsChanged;
        _syncState.Changed += OnSyncStateChanged;
        ShowPresence();
        ShowPermissions();
        ShowSyncState();
        ShowWhetherToOfferReconnecting();
    }

    private void OnPresenceChanged(object? sender, EventArgs e) => ShowPresence();

    private void OnPermissionsChanged(object? sender, EventArgs e) => ShowPermissions();

    /// <summary>
    /// A sync that just failed is one of the two states worth offering Reconnect in, so this asks the
    /// whole question rather than only redrawing the line - see ShowWhetherToOfferReconnecting.
    /// </summary>
    private void OnSyncStateChanged(object? sender, EventArgs e) => ShowWhetherToOfferReconnecting();

    private void ShowSyncState()
    {
        // A phone with no network says so, whatever the last sync happened to conclude. Otherwise the
        // row reads "Synced" next to a button offering to reconnect, which is two answers to one
        // question - and "Synced" is the wrong one: it was true when it was said and is not now.
        if (!_networkStatus.IsOnline)
        {
            SyncLabel = _translations["No connection"];
            IsSyncing = false;
            return;
        }

        SyncLabel = _syncState.Condition switch
        {
            SyncCondition.Syncing => _translations["Syncing…"],
            SyncCondition.Synced => _translations["Synced"],
            // Not "Offline": that word is already the answer to "is this person there", and one English
            // string cannot be both - Polish has a different word for each ("Niedostępny" about somebody,
            // "Bez połączenia" about the app). This row is about the connection, so it says so.
            SyncCondition.Offline => _translations["No connection"],
            SyncCondition.Failed => _translations["Couldn't sync"],
            // Before anything has tried, saying "Synced" would be a claim and saying "Offline" a
            // slander. The row stays quiet instead.
            _ => string.Empty
        };

        IsSyncing = _syncState.Condition == SyncCondition.Syncing;
    }

    /// <summary>
    /// Which sections this account can reach at all. Hidden rather than shown-and-refused, as the web's
    /// sidebar does it: a link that only ever leads to "not unlocked" is not a link.
    /// </summary>
    private void ShowPermissions()
    {
        CanUseLocation = _permissions.Has(ApplicationPermission.Location);
        CanUseConversations = _permissions.Has(ApplicationPermission.Chat)
            || _permissions.Has(ApplicationPermission.Contacts);
    }

    /// <summary>
    /// The dot in the avatar's top-right corner. Kept as the four states rather than a colour, so what
    /// the app decided stays testable and only the page turns it into a colour.
    /// </summary>
    private void ShowPresence() => Appearance = _presence.Appearance;

    public bool HasUnread => UnreadLabel.Length > 0;

    /// <summary>
    /// Whether a newer Orbit is out. Read from what startup already learned rather than asked again -
    /// see MobileVersionGate - so opening a screen costs nothing and the answer survives being offline.
    ///
    /// Startup says it once, in a prompt the reader is free to dismiss; this is what remains afterwards,
    /// and the only standing sign that there is anything to do about it.
    /// </summary>
    [ObservableProperty]
    private bool _isUpdateAvailable;

    partial void OnUnreadLabelChanged(string value) => OnPropertyChanged(nameof(HasUnread));

    /// <summary>
    /// Fills in who is signed in, then asks about unread notifications. The initials come from the
    /// stored session so the bar is complete offline; only the badge needs the server, and its absence
    /// is not worth a message - a missing badge reads as "nothing new", which is the safer wrong answer.
    /// </summary>
    [RelayCommand]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        _presence.MarkActive();
        DisplayName = (await _sessionStore.GetAsync())?.DisplayName ?? string.Empty;
        Initials = InitialsOf(DisplayName);

        // Shared with whatever screen is behind the bar, so the two cannot disagree and only one
        // request is made between them.
        await _permissions.EnsureLoadedAsync(cancellationToken);

        IsUpdateAvailable = await _versionGate.RememberedDecisionAsync(cancellationToken) is { OffersUpdate: true };
        await ShowWhatIsWaitingToBeDecidedAsync(cancellationToken);

        try
        {
            UnreadLabel = FormatCount((await _notificationsClient.GetUnreadAsync(cancellationToken)).Count);
        }
        catch (HttpRequestException)
        {
        }
        catch (OperationCanceledException)
        {
        }
    }

    /// <summary>
    /// How many copies taken offline are still waiting to be chosen between. Counted from the phone, so
    /// it is right with no connection - which is the state they were made in.
    /// </summary>
    private async Task ShowWhatIsWaitingToBeDecidedAsync(CancellationToken cancellationToken)
    {
        var waiting = 0;
        foreach (var store in _copyStores)
        {
            waiting += (await store.GetCopiesAwaitingReviewAsync(cancellationToken)).Count;
        }

        CopiesAwaitingReview = waiting;
    }

    /// <summary>What is waiting to be decided, badged in the menu the way notifications are.</summary>
    [ObservableProperty]
    private int _copiesAwaitingReview;

    public bool HasCopiesAwaitingReview => CopiesAwaitingReview > 0;

    public string CopiesAwaitingReviewLabel => CopiesAwaitingReview.ToString();

    partial void OnCopiesAwaitingReviewChanged(int value)
    {
        OnPropertyChanged(nameof(HasCopiesAwaitingReview));
        OnPropertyChanged(nameof(CopiesAwaitingReviewLabel));
    }

    [RelayCommand]
    private void GoToCopyReview()
    {
        IsMenuOpen = false;
        _navigator.ShowCopyReview();
    }

    /// <summary>
    /// Whether to offer trying again. Only while the phone believes it is offline: online there is
    /// nothing to reconnect, and a button that is always there invites tapping at a working app.
    /// </summary>
    [ObservableProperty]
    private bool _canReconnect;

    /// <summary>
    /// Tries the server again, now, rather than waiting for whatever would have tried next.
    ///
    /// It cannot put the phone back on a network - no app can - so what it actually does is attempt the
    /// work that being offline prevented. That is the useful half: a phone whose connection came back
    /// without the system noticing, or one behind a portal that has just been signed into, is in step
    /// again afterwards, and the corner says so instead of "No connection" until something else asks.
    /// </summary>
    [RelayCommand]
    private async Task ReconnectAsync(CancellationToken cancellationToken)
    {
        await _synchronizer.SynchroniseAsync(cancellationToken);
        await _permissions.RefreshAsync(cancellationToken);
        ShowWhetherToOfferReconnecting();
    }

    /// <summary>
    /// Offered whenever trying again could help: with no connection, and equally when the phone had one
    /// and the attempt failed anyway. "Couldn't sync" with nothing to tap was the worse of the two -
    /// being told something went wrong and given no way to do anything about it.
    /// </summary>
    private void ShowWhetherToOfferReconnecting()
    {
        CanReconnect = !_networkStatus.IsOnline || _syncState.Condition is SyncCondition.Failed;
        ShowSyncState();
    }

    [RelayCommand]
    private void GoToDashboard() => _navigator.ShowDashboard();

    [RelayCommand]
    private void GoToNotes() => _navigator.ShowNotes();

    [RelayCommand]
    private void GoToTasks() => _navigator.ShowTasks();

    [RelayCommand]
    private void GoToCalendar() => _navigator.ShowCalendar();

    [RelayCommand]
    private void GoToInventory() => _navigator.ShowInventory();

    [RelayCommand]
    private void GoToMap() => _navigator.ShowMap();

    [RelayCommand]
    private void GoToContacts() => _navigator.ShowContacts();

    /// <summary>
    /// The avatar opens a menu rather than going anywhere, the same as Orbit.Web's: the account, the
    /// notifications and signing out all hang off it, and making the avatar mean one of them would hide
    /// the other two.
    /// </summary>
    [RelayCommand]
    private async Task ToggleMenuAsync(CancellationToken cancellationToken)
    {
        _presence.MarkActive();
        IsMenuOpen = !IsMenuOpen;
        if (!IsMenuOpen)
        {
            IsStatusExpanded = false;
            IsLanguageExpanded = false;
            return;
        }

        // Counted again on the way open rather than only when the bar loaded. Answering a review is the
        // one thing that changes this number without leaving the screen, and a badge still claiming one
        // waiting, on the menu the reader has just used to answer it, reads as an answer that failed.
        await ShowWhatIsWaitingToBeDecidedAsync(cancellationToken);
    }

    /// <summary>
    /// Public because the navigator calls it on every screen change, not only the menu's own dismiss
    /// button: the bar is one shared instance across every page, so a menu left open outlives the
    /// screen it was opened over. Orbit.Web closes both its menus from MainLayout's LocationChanged
    /// handler for exactly this reason.
    /// </summary>
    [RelayCommand]
    public void CloseMenu()
    {
        IsMenuOpen = false;
        // Folded away with the menu, so the next visit opens on the list rather than mid-choice.
        IsStatusExpanded = false;
        IsLanguageExpanded = false;
    }

    [RelayCommand]
    private void ToggleStatus() => IsStatusExpanded = !IsStatusExpanded;

    [RelayCommand]
    private void ToggleLanguage() => IsLanguageExpanded = !IsLanguageExpanded;

    public bool IsEnglish => _translations.Language == AppLanguage.English;

    public bool IsPolish => !IsEnglish;

    /// <summary>What the folded row shows on its right, so the choice is readable unopened.</summary>
    public string LanguageDescription => IsEnglish ? "English" : "Polski";

    [RelayCommand]
    private void ChooseEnglish() => ChooseLanguage(AppLanguage.English);

    [RelayCommand]
    private void ChoosePolish() => ChooseLanguage(AppLanguage.Polish);

    /// <summary>
    /// Closes the menu and shows the current screen again. Every string is resolved when a page is
    /// built, so the language only takes effect on the next page - and leaving the reader looking at
    /// the old language after choosing a new one would read as the choice not having worked.
    /// </summary>
    private void ChooseLanguage(AppLanguage language)
    {
        _translations.SetLanguage(language);
        OnPropertyChanged(nameof(IsEnglish));
        OnPropertyChanged(nameof(IsPolish));
        OnPropertyChanged(nameof(LanguageDescription));
        IsLanguageExpanded = false;
        IsMenuOpen = false;
        _navigator.ShowDashboard();
    }

    [RelayCommand]
    private void GoToAccount() => LeaveMenuFor(_navigator.ShowAccount);

    /// <summary>
    /// Which build this is, when it was made, and under what licence - the phone's answer to Orbit.Web's
    /// footer. Folded into the menu rather than given a screen of its own, the way Status and Language
    /// already are: it is three lines somebody reads once, and a page for it would be a page nobody
    /// navigates back from having learned anything more.
    /// </summary>
    [ObservableProperty]
    private bool _isAboutExpanded;

    [RelayCommand]
    private async Task ToggleAboutAsync()
    {
        IsAboutExpanded = !IsAboutExpanded;
        if (IsAboutExpanded)
        {
            await ReadTheServerVersionAsync();
        }
    }

    public string AboutCopyright => OrbitRelease.Copyright;

    /// <summary>
    /// This build, read off this assembly rather than off Orbit.Core's - the number is per client, and
    /// the shared project is compiled into three of them. See OrbitVersion.
    /// </summary>
    private static readonly OrbitVersion Build = OrbitVersion.ReadFrom(typeof(NavigationBarViewModel).Assembly);

    private readonly ServerVersionClient _serverVersion;

    /// <summary>
    /// Which build of the server this app is talking to, once it has been asked. Empty until then and
    /// when it cannot be reached - see ServerVersionClient, and ServerVersionDto for why the two versions
    /// are worth showing separately.
    /// </summary>
    [ObservableProperty]
    private string _aboutServerVersion = string.Empty;

    public bool HasServerVersion => AboutServerVersion.Length > 0;

    /// <summary>
    /// Asked when the About row is opened rather than at startup: it is one line in a menu, and paying
    /// for it on every launch would be paying for something most launches never show.
    /// </summary>
    private async Task ReadTheServerVersionAsync()
    {
        if (HasServerVersion || await _serverVersion.GetAsync() is not { } server)
        {
            return;
        }

        AboutServerVersion = server.CommitHash.Length == 0
            ? $"api ver:{server.Version}"
            : $"api ver:{server.Version}+gitHash:{Shorten(server.CommitHash)}";
        OnPropertyChanged(nameof(HasServerVersion));
    }

    private static string Shorten(string commitHash) => commitHash.Length > 7 ? commitHash[..7] : commitHash;

    /// <summary>Whether the row is showing the whole commit hash rather than the first seven of it.</summary>
    [ObservableProperty]
    private bool _isWholeCommitShown;

    public string AboutVersion => IsWholeCommitShown ? Build.Full : Build.Short;

    /// <summary>
    /// Whether tapping the version does anything. False in a released build, where the commit is not
    /// part of what the app says about itself - see OrbitVersion.
    /// </summary>
    public bool CanShowTheWholeCommit => Build.CanShowTheWholeCommit;

    /// <summary>
    /// Tapping the version grows the rest of the hash while debugging. The short form is what anybody
    /// reads; the whole one is what a `git checkout` takes, and asking for it should not mean going
    /// somewhere else.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanShowTheWholeCommit))]
    private void ShowTheWholeCommit()
    {
        IsWholeCommitShown = !IsWholeCommitShown;
        OnPropertyChanged(nameof(AboutVersion));
    }

    public string LicenseName => _translations[OrbitRelease.LicenseName];

    /// <summary>Where the licence itself can be read - opened outside the app, see AvatarMenu's code-behind.</summary>
    public string LicenseUrl => OrbitRelease.LicenseUrl;

    [RelayCommand]
    private void GoToNotifications() => LeaveMenuFor(_navigator.ShowNotifications);

    /// <summary>Where a newer Orbit comes from - Orbit.Web's "Get the app", called what it is here.</summary>
    [RelayCommand]
    private void GoToUpdate() => LeaveMenuFor(_navigator.ShowUpdate);

    /// <summary>
    /// Notification settings, reached from here rather than from the notification list. They are the
    /// account's settings, and the list is a list.
    /// </summary>

    /// <summary>Whether the reader has chosen to be shown as available - see Presence for the other two states.</summary>
    public bool IsAvailable => _presence.Chosen == ChosenAvailability.Available;

    public bool IsUnavailable => !IsAvailable;

    /// <summary>What the collapsed row says on its right-hand side, so the choice is readable unopened.</summary>
    public string ChosenDescription => _translations[IsAvailable ? "Available" : "Unavailable"];

    [RelayCommand]
    private void ChooseAvailable() => Choose(ChosenAvailability.Available);

    [RelayCommand]
    private void ChooseUnavailable() => Choose(ChosenAvailability.Unavailable);

    /// <summary>
    /// Deliberately leaves the *menu* open, though it folds the choices away. Setting a status is not
    /// leaving the menu, and closing it would hide the dot the reader just changed before they could see
    /// it change.
    /// </summary>
    private void Choose(ChosenAvailability availability)
    {
        _presence.Choose(availability);
        OnPropertyChanged(nameof(IsAvailable));
        OnPropertyChanged(nameof(IsUnavailable));
        OnPropertyChanged(nameof(ChosenDescription));
        // Folds back up: the choice is made, and the row now shows it.
        IsStatusExpanded = false;
    }

    [RelayCommand]
    private async Task SignOutAsync()
    {
        IsMenuOpen = false;
        await _authenticationClient.SignOutAsync();
        // Everything cached belonged to whoever just left. Guid.Empty marks the database as nobody's,
        // so the next sign-in finds it already clear.
        await _localStore.ClearForAsync(Guid.Empty);
        _permissions.Forget();
        _navigator.ShowSignIn();
    }

    /// <summary>The menu closes on the way out, so coming back does not find it hanging open.</summary>
    private void LeaveMenuFor(Action show)
    {
        IsMenuOpen = false;
        show();
    }

    /// <summary>
    /// Two initials at most, worked out the way Orbit.Web works them out - see its AvatarHelper. The same
    /// person has to read the same on both, and the avatar is on every screen, so a rule of its own here
    /// showed up everywhere: a one-word name came out a single letter on the phone and two letters in the
    /// browser, and a three-word name took a different second letter.
    ///
    /// The one deliberate difference is an empty name, which the web renders as "?" and this leaves
    /// blank: an avatar reading "?" looks like a fault rather than an unnamed account.
    /// </summary>
    private static string InitialsOf(string? displayName)
    {
        var words = (displayName ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return words switch
        {
            [] => string.Empty,
            [var only] => (only.Length >= 2 ? only[..2] : only[..1]).ToUpperInvariant(),
            [var first, var second, ..] => $"{first[..1]}{second[..1]}".ToUpperInvariant()
        };
    }

    /// <summary>
    /// Capped the way the web caps it: past a certain point the exact number stops being information
    /// and starts being a number too wide for the badge.
    /// </summary>
    private static string FormatCount(int unread) => unread switch
    {
        <= 0 => string.Empty,
        > 99 => "99+",
        _ => unread.ToString()
    };
}
