using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Mobile.Authentication;
using Orbit.Mobile.Crypto;
using Orbit.Mobile.Data;
using System.Collections.ObjectModel;
using System.Text;
using Orbit.Core.Permissions;
using Orbit.Mobile.Google;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Api;
using Orbit.Mobile.Permissions;
using Orbit.Mobile.Sync;

namespace Orbit.Mobile.Screens.Account;

/// <summary>
/// Changing the three things that identify an account: username, email address, and password.
///
/// All of them need a connection, and none of them is queued - see <see cref="AccountClient"/> for why.
/// The screen says so up front and disables the actions rather than accepting a change it cannot make,
/// because the alternative is telling someone their password changed while the old one still works.
/// </summary>
public sealed partial class AccountViewModel : ObservableObject
{
    private readonly AccountClient _accountClient;
    private readonly GoogleExtras _googleExtras;
    private readonly OwnEncryptionKeyProvider _encryptionKeyProvider;
    private readonly SessionStore _sessionStore;
    private readonly Translations _translations;
    private readonly UsersClient _usersClient;
    private readonly UserPermissions _permissions;
    private readonly IThemeStore _themes;
    private readonly IAccentColorStore _accents;
    private readonly TransferClient _transfer;
    private readonly LocalStoreReset _localStore;
    private readonly IScreenNavigator _navigator;

    [ObservableProperty]
    private string _userName = string.Empty;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string _newEmailAddress = string.Empty;

    [ObservableProperty]
    private string _emailConfirmationCode = string.Empty;

    [ObservableProperty]
    private string _currentPassword = string.Empty;

    [ObservableProperty]
    private string _newPassword = string.Empty;

    /// <summary>
    /// The new password typed a second time. A password nobody can read back is a password nobody can
    /// check, and one mistyped here is one nobody can sign in with afterwards - Orbit.Web asks for it in
    /// the same form, and this screen took the first answer and changed the password to it.
    /// </summary>
    [ObservableProperty]
    private string _repeatedNewPassword = string.Empty;

    /// <summary>
    /// Confirms the deletion below. Kept apart from <see cref="CurrentPassword"/> deliberately: typing a
    /// password into the change-password box and then pressing a delete button that silently reused it
    /// is the one mistake this screen must not make possible.
    /// </summary>
    [ObservableProperty]
    private string _deleteAccountPassword = string.Empty;

    /// <summary>
    /// What an account without a password types before it is deleted - its email address or its login.
    /// See <see cref="ConfirmsAccount"/>.
    /// </summary>
    [ObservableProperty]
    private string _deleteAccountConfirmation = string.Empty;

    /// <summary>
    /// What the last attempt to delete said. Its own line, inside the danger card, rather than the shared
    /// one at the top of the screen: the reader is at the foot of a long scroll when they press Delete,
    /// and a refusal printed where they cannot see it reads as a button that did nothing.
    /// </summary>
    [ObservableProperty]
    private string _deletionMessage = string.Empty;

    /// <summary>
    /// The account as the server last described it, or null until it has been read. Deleting is refused
    /// while it is null: without it there is no telling what to ask for, and guessing "nothing" is the
    /// one wrong guess that deletes something. The form's own <see cref="UserName"/> is not a substitute
    /// - it is the box the reader edits.
    /// </summary>
    private Orbit.Contracts.Users.AccountDto? _account;

    /// <summary>The address the account signs in with today, which the form below changes.</summary>
    [ObservableProperty]
    private string _emailAddress = string.Empty;

    [ObservableProperty]
    private bool _isEmailVerified;

    /// <summary>
    /// Whether deleting needs the password. False for a Google account that never set one - being signed
    /// in is the proof there, and DeleteAccountCommandHandler says so on the server. True until the
    /// account has actually been read: asking for a password that turns out not to be needed is a
    /// nuisance, while not asking when it is needed looks like the deletion silently failed. Nothing is
    /// sent before then either way - see <see cref="IsReadyToDelete"/>.
    /// </summary>
    [ObservableProperty]
    private bool _requiresPasswordToDelete = true;

    /// <summary>
    /// Whether deleting needs the address or login typed out instead. Only once the account is known to
    /// have no password: an account with none has nothing to prove itself with, and the server asks it
    /// for nothing, so typing this is what stands between one stray press and everything it holds.
    /// </summary>
    public bool RequiresTypedAccountToDelete => _account is { HasPassword: false } && !ConfirmsWithGoogle;

    /// <summary>
    /// Whether an account without a password proves it is the owner with Google rather than by typing its
    /// address: it is linked to Google, and this deployment offers Google to this app. Google is asked
    /// again when Delete is pressed, and the server checks the sign-in is this account's and a fresh one -
    /// which a phone left unlocked somewhere could not fake the way it could type an address. Orbit.Web
    /// asks the same way (see Options' danger zone).
    /// </summary>
    public bool ConfirmsWithGoogle => _account is { HasPassword: false, IsGoogleLinked: true } && GoogleLink.IsOffered;

    /// <summary>
    /// Whether to say which password is meant. An account that signs in with Google can hold one without
    /// thinking of itself as having one - chat made it set one, or it had one before Google was connected
    /// - and a bare "Password" left it asking for something it did not recognise. Orbit.Web says the same.
    /// </summary>
    public bool ExplainsWhichPassword => _account is { HasPassword: true, IsGoogleLinked: true };

    public bool HasDeletionMessage => DeletionMessage.Length > 0;

    /// <summary>
    /// "Verified" or "Not verified" - the same pair Orbit.Web shows beside the address. One label whose
    /// text changes rather than two that take turns being hidden.
    /// </summary>
    public string EmailVerificationLabel
        => _translations[IsEmailVerified ? "Verified" : "Not verified"];

    [ObservableProperty]
    private string _message = string.Empty;

    [ObservableProperty]
    private bool _messageIsFailure;

    [ObservableProperty]
    private string _permissionCode = string.Empty;

    /// <summary>
    /// What the code did. Its own line rather than the shared one above, because unlocking something is
    /// a different subject from changing a password and the two overwriting each other reads as a bug.
    /// </summary>
    [ObservableProperty]
    private string _permissionMessage = string.Empty;

    [ObservableProperty]
    private bool _isRedeemingCode;

    public AccountViewModel(
        AccountClient accountClient, OwnEncryptionKeyProvider encryptionKeyProvider, ConnectionRequirement connection,
        SessionStore sessionStore, Translations translations, UsersClient usersClient,
        UserPermissions permissions, IThemeStore themes, IAccentColorStore accents, TransferClient transfer,
        LocalStoreReset localStore,
        Notifications.NotificationSettingsViewModel notifications, IScreenNavigator navigator,
        GoogleAccountLink googleLink, GoogleExtras googleExtras)
    {
        _accountClient = accountClient;
        _encryptionKeyProvider = encryptionKeyProvider;
        Connection = connection;
        // The button answers to both of them: something to export, and a connection to ask for it over.
        Export.PropertyChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(CanExport));
            OnPropertyChanged(nameof(PlacesWarning));
            OnPropertyChanged(nameof(HasPlacesWarning));
        };
        connection.PropertyChanged += (_, _) => OnPropertyChanged(nameof(CanExport));
        _sessionStore = sessionStore;
        _translations = translations;
        _usersClient = usersClient;
        _permissions = permissions;
        _themes = themes;
        _accents = accents;
        _transfer = transfer;
        _localStore = localStore;
        _theme = themes.Read();
        _accent = accents.Read();
        Notifications = notifications;
        _navigator = navigator;
        GoogleLink = googleLink;
        _googleExtras = googleExtras;
        // Connecting or disconnecting changes what the account is, so the screen reads it again rather
        // than keeping the copy it showed before.
        GoogleLink.Changed += (_, _) => _ = ShowAccountAsync();
    }

    /// <summary>
    /// How Orbit is allowed to interrupt, which Orbit.Web keeps in this same Options page under
    /// Appearance. It had a screen of its own here, reached from a menu entry called "Settings" beside
    /// one called "Account" - two doors to the same room, and neither name said which.
    /// </summary>
    public Notifications.NotificationSettingsViewModel Notifications { get; }

    /// <summary>Signing in with Google as well as with a password - see <see cref="GoogleAccountLink"/>.</summary>
    public GoogleAccountLink GoogleLink { get; }

    /// <summary>
    /// Whether this phone offers the links that hand an event to Google Calendar or a place to Google
    /// Maps. Kept on the device, and a different question from the account below it: turning these off
    /// leaves a connected Google account connected, and signing in with it still works.
    /// </summary>
    public bool AllowsGoogleExtras
    {
        get => _googleExtras.IsAllowedOnThisDevice;
        set
        {
            if (value == _googleExtras.IsAllowedOnThisDevice)
            {
                return;
            }

            _googleExtras.IsAllowedOnThisDevice = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Whether the switch above is worth offering: an account that has neither confirmed an address nor
    /// connected Google cannot use the extras at all - see GoogleIntegrationAccess.
    /// </summary>
    [ObservableProperty]
    private bool _canChooseGoogleExtras;

    /// <summary>
    /// Which section is showing. Tabs rather than one long scroll, as Orbit.Web's Options page has -
    /// changing a password and unlocking a feature are different errands and were stacked on top of
    /// each other.
    /// </summary>
    [ObservableProperty]
    private AccountTab _tab = AccountTab.Account;

    /// <summary>
    /// The tabs this account is offered. Everything but the Debugger, which goes only to an account
    /// that has unlocked it: what it opens is Orbit's own inside - the captured log and the detail
    /// behind an error - and that is the same line the browser's Options draws and the same one the
    /// version row in the avatar menu draws. See ApplicationPermission.Debug.
    /// </summary>
    public IReadOnlyList<AccountTabRow> Tabs
        => [.. Enum.GetValues<AccountTab>()
            .Where(IsOffered)
            .Select(tab => new AccountTabRow(tab, AccountTabRow.Describe(tab, _translations), tab == Tab))];

    private bool IsOffered(AccountTab tab)
        => tab != AccountTab.Debug || _permissions.Has(ApplicationPermission.Debug);

    public bool IsShowingAccount => Tab is AccountTab.Account;

    public bool IsShowingAppearance => Tab is AccountTab.Appearance;

    public bool IsShowingPermissions => Tab is AccountTab.Permissions;

    /// <summary>
    /// The section behind the Debugger tab. Answers to the permission as well as to the tab, so an
    /// account that loses it while the screen is open is not left reading what it no longer holds.
    /// </summary>
    public bool IsShowingDebug => Tab is AccountTab.Debug && _permissions.Has(ApplicationPermission.Debug);

    [RelayCommand]
    private void ChooseTab(AccountTabRow? row)
    {
        if (row is not null)
        {
            Tab = row.Tab;
        }
    }

    /// <summary>What the last export or import did, or why it did not.</summary>
    [ObservableProperty]
    private string _transferMessage = string.Empty;

    [ObservableProperty]
    private bool _isTransferring;

    public bool HasTransferMessage => TransferMessage.Length > 0;

    /// <summary>
    /// What the last export could not do although it wrote the file - private places this phone could
    /// not open. Apart from <see cref="TransferMessage"/>, and in the warning colour, because it is about
    /// what is missing from a file that otherwise looks complete.
    /// </summary>
    [ObservableProperty]
    private string _transferWarning = string.Empty;

    public bool HasTransferWarning => TransferWarning.Length > 0;

    /// <summary>
    /// Said beside the places switch while it is on, and only then: places are the one part of the file
    /// that is not sealed, and a warning printed under every export would be read as boilerplate by the
    /// time it mattered. Empty while places are left out.
    /// </summary>
    public string PlacesWarning
        => Export.IncludesPlaces
            ? _translations["Places are written to the file decrypted. The file itself is not encrypted, so anyone who gets it can read every place in it - names, addresses, coordinates and descriptions - private ones included."]
            : string.Empty;

    public bool HasPlacesWarning => PlacesWarning.Length > 0;

    /// <summary>What the file picker is titled - the picker is the platform's, the words are the app's.</summary>
    public string ImportPickerTitle => _translations["Import"];

    /// <summary>
    /// Raised with the file's name and its contents once an export is built. Writing it and handing it
    /// somewhere is a platform call - the share sheet - and reaching for one here is what would make
    /// this screen untestable.
    /// </summary>
    public event EventHandler<(string FileName, string Json)>? ExportReady;

    /// <summary>
    /// What the next export will carry - the same five parts the browser offers, every one but places
    /// unless the reader says otherwise. See ExportChoice.
    /// </summary>
    public ExportChoice Export { get; } = new();

    /// <summary>
    /// Whether there is an export to build: something chosen, and a connection to ask for it over -
    /// the archive is the server's answer rather than something this phone can assemble.
    /// </summary>
    public bool CanExport => Connection.IsMet && !Export.IsEmpty;

    [RelayCommand]
    private async Task ExportAsync(CancellationToken cancellationToken)
    {
        IsTransferring = true;
        TransferWarning = string.Empty;
        try
        {
            var everything = await _transfer.ExportAsync(cancellationToken);
            if (everything is null)
            {
                TransferMessage = _translations["Couldn't build the export. Try again."];
                return;
            }

            // Places are opened after narrowing, so an export that left them out never reaches for the key.
            var opened = await _transfer.OpenPlacesAsync(Export.Narrow(everything), cancellationToken);
            var archive = opened.Archive;
            // Said rather than left to the file: what was asked for and what came back are two different
            // things, and a file nobody opens is where that difference would otherwise be found.
            TransferMessage = _translations.Format(
                "Exported {0} notes, {1} task lists, {2} events, {3} inventories and {4} places.",
                archive.Notes.Count, archive.TaskLists.Count, archive.CalendarEvents.Count,
                archive.Inventories.Count, archive.AllPlaces.Count);
            if (opened.UnopenedPlaces > 0)
            {
                // Written anyway rather than refused: the rest of the file is still worth having, and
                // what is missing from it is said here rather than found when the file is opened.
                TransferWarning = _translations.Format(
                    "{0} private places couldn't be opened on this phone and were written without their content.",
                    opened.UnopenedPlaces);
            }

            ExportReady?.Invoke(
                this, ($"orbit-export-{DateTimeOffset.Now:yyyy-MM-dd}.json", _transfer.Write(archive)));
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            TransferMessage = _translations["Couldn't build the export. Try again."];
        }
        finally
        {
            IsTransferring = false;
        }
    }


    /// <summary>
    /// The largest file this will read. An export of a whole account is not large by file standards, but
    /// a hand-made one could be - and here the whole thing becomes a single string in a phone's memory
    /// before anything looks at it. The same ceiling Orbit.Web enforces on its own picker.
    /// </summary>
    public const long MaximumImportSizeBytes = 32 * 1024 * 1024;

    /// <summary>
    /// Reads the picked file and imports it, refusing one too large to hold. Takes the stream rather
    /// than the text so that a file over the ceiling is never turned into a string at all - which is
    /// the thing being guarded against.
    /// </summary>
    public async Task ImportAsync(Stream file, CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(file);
        var buffer = new char[8192];
        var json = new StringBuilder();
        int read;
        while ((read = await reader.ReadAsync(buffer, cancellationToken)) > 0)
        {
            json.Append(buffer, 0, read);
            if (json.Length > MaximumImportSizeBytes)
            {
                TransferMessage = _translations["That file is too large to import."];
                return;
            }
        }

        await ImportAsync(json.ToString(), cancellationToken);
    }

    /// <summary>
    /// Reads a file the reader picked. Importing creates new things rather than restoring old ones, so
    /// running it into an account that already has things in it puts none of them at risk.
    /// </summary>
    public async Task ImportAsync(string json, CancellationToken cancellationToken = default)
    {
        IsTransferring = true;
        try
        {
            var result = await _transfer.ImportAsync(json, cancellationToken);
            TransferMessage = result is null
                ? _translations["That file didn't contain an Orbit export."]
                : _translations.Format(
                    "Imported {0} notes, {1} task lists, {2} events, {3} inventories and {4} places.",
                    result.Notes, result.TaskLists, result.CalendarEvents, result.Inventories, result.Places);
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            TransferMessage = _translations["Couldn't import that file. Try again."];
        }
        finally
        {
            IsTransferring = false;
        }
    }

    /// <summary>How Orbit looks on this device - see ChosenTheme.</summary>
    [ObservableProperty]
    private ChosenTheme _theme;

    /// <summary>
    /// The three of them, in the order the enum declares - System first, because it is what Orbit did
    /// before there was a choice at all. All three are on screen at once, which is why this is a strip
    /// and not a list to open: choosing is one press, and the two not chosen are part of the answer.
    /// </summary>
    public IReadOnlyList<ThemeChoice> Themes
        => [.. Enum.GetValues<ChosenTheme>()
            .Select(theme => new ThemeChoice(theme, ThemeChoice.Describe(theme, _translations), theme == Theme))];

    /// <summary>The colour Orbit highlights things in - kept on this device, like the theme.</summary>
    [ObservableProperty]
    private AccentColor _accent;

    /// <summary>
    /// Every colour on offer, each with the swatch it paints as. The swatch is worked out here rather
    /// than left to the markup: the accent tokens differ between the light and dark themes, and a row
    /// of swatches that ignored that would show the reader colours the app would not actually use.
    /// </summary>
    public IReadOnlyList<AccentChoice> Accents
        => [.. AccentColor.All.Select(accent => new AccentChoice(
            accent, _translations[accent.Name], AccentPalette.For(accent.Hue, IsDarkOnScreen).Accent,
            accent == Accent))];

    /// <summary>
    /// Which theme the swatches are painted for. Set by the app head, which is the only thing that
    /// knows - "System" means whatever the phone is doing, and this project cannot ask a phone.
    /// </summary>
    public bool IsDarkOnScreen
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged(nameof(Accents));
        }
    }

    /// <summary>What this account may use, and what it would take to unlock the rest.</summary>
    public ObservableCollection<PermissionRow> Permissions { get; } = [];

    public bool HasPermissionMessage => PermissionMessage.Length > 0;

    /// <summary>
    /// Everything on this screen needs the server - a username has to be checked for being free, a
    /// password change has to be proved against the old one - so the whole form reflects one answer.
    /// Held as an object rather than read from the network each time, because the answer changes while
    /// somebody is filling the form in and the buttons have to follow it.
    /// </summary>
    public ConnectionRequirement Connection { get; }

    public bool HasMessage => Message.Length > 0;

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (await _sessionStore.GetAsync() is { } session)
        {
            DisplayName = session.DisplayName;
        }

        await ShowAccountAsync();

        await _permissions.EnsureLoadedAsync();
        ShowPermissions();
        await Notifications.LoadCommand.ExecuteAsync(null);
    }

    /// <summary>
    /// Who this account actually is, as the server holds it: the username and address the forms below
    /// change, whether the address has been confirmed, and whether there is a password to prove before
    /// deleting. Read rather than taken from the session, which carries only what signing in needed and
    /// goes stale the moment any of it is changed on another device.
    ///
    /// Best-effort on purpose. Offline the screen still opens, showing what the session knows - the
    /// alternative is a settings screen that refuses to appear because a request failed.
    /// </summary>
    private async Task ShowAccountAsync()
    {
        try
        {
            if (await _accountClient.GetAccountAsync() is not { } account)
            {
                return;
            }

            _account = account;
            UserName = account.UserName;
            DisplayName = account.DisplayName;
            EmailAddress = account.Email;
            IsEmailVerified = account.IsEmailVerified;
            RequiresPasswordToDelete = account.HasPassword;
            OnPropertyChanged(nameof(RequiresTypedAccountToDelete));
            OnPropertyChanged(nameof(ExplainsWhichPassword));
            // A switch for something the account cannot use yet would turn nothing off, so it is only
            // offered where the account qualifies - the line Orbit.Web draws over the same row.
            CanChooseGoogleExtras = GoogleIntegrationAccess.Qualifies(account);
            await GoogleLink.ShowAsync(account);
            // After the Google row has asked whether Google is offered here, which is half of the answer.
            OnPropertyChanged(nameof(ConfirmsWithGoogle));
            OnPropertyChanged(nameof(RequiresTypedAccountToDelete));
        }
        catch (HttpRequestException)
        {
            // Offline, or the request failed. What the session knows is still on screen.
        }
    }

    /// <summary>
    /// The server answers the same way for a code that matched nothing and one that came too early, so
    /// the message here is the only place the difference is said out loud - to whoever typed it.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanRedeemCode))]
    private async Task RedeemCodeAsync(CancellationToken cancellationToken)
    {
        IsRedeemingCode = true;
        try
        {
            var outcome = await _usersClient.RedeemPermissionCodeAsync(PermissionCode.Trim(), cancellationToken);
            PermissionCode = string.Empty;

            if (outcome.MissingPrerequisite is { } missing
                && Enum.TryParse<ApplicationPermission>(missing, out var required))
            {
                PermissionMessage = _translations.Format(
                    "{0} has to be unlocked first.", LockedFeatureMessage.Describe(required, _translations));
            }
            else if (outcome.Granted is { } granted && Enum.TryParse<ApplicationPermission>(granted, out var permission))
            {
                PermissionMessage = _translations.Format(
                    "{0} is unlocked.", LockedFeatureMessage.Describe(permission, _translations));
            }
            else
            {
                PermissionMessage = _translations["That code doesn't unlock anything."];
            }

            await _permissions.RefreshAsync(cancellationToken);
            ShowPermissions();
        }
        catch (HttpRequestException)
        {
            PermissionMessage = _translations["Couldn't reach Orbit. Check your connection and try again."];
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            IsRedeemingCode = false;
        }
    }

    private bool CanRedeemCode => PermissionCode.Trim().Length > 0 && !IsRedeemingCode && Connection.IsMet;

    private void ShowPermissions()
    {
        var granted = _permissions.Granted;

        Permissions.Clear();
        foreach (var permission in Enum.GetValues<ApplicationPermission>())
        {
            // Debugger is not listed until it has been unlocked - see PermissionListing, the one rule
            // this and Orbit.Web's own table both read. The code box below is always here, which is
            // what the code for it is typed into.
            if (!PermissionListing.IsListed(permission, granted))
            {
                continue;
            }

            Permissions.Add(PermissionRow.For(permission, granted, _translations));
        }

        // Unlocking Debug adds a tab, and losing it takes one away - so the row of tabs is drawn again
        // rather than left as it was when the screen opened. A reader standing in a tab they no longer
        // hold is put back where everybody starts.
        if (!IsOffered(Tab))
        {
            Tab = AccountTab.Account;
        }

        OnPropertyChanged(nameof(Tabs));
        OnPropertyChanged(nameof(IsShowingDebug));
    }

    [RelayCommand]
    private Task ChangeUserNameAsync(CancellationToken cancellationToken)
        => RunAsync(
            () => _accountClient.ChangeUserNameAsync(UserName.Trim(), DisplayName.Trim(), cancellationToken),
            "Username updated.");

    [RelayCommand]
    private Task RequestEmailChangeAsync(CancellationToken cancellationToken)
        => RunAsync(
            () => _accountClient.RequestEmailAddressChangeAsync(NewEmailAddress.Trim(), cancellationToken),
            "Check the new address for a confirmation code - the change isn't done until you enter it.");

    [RelayCommand]
    private Task ConfirmEmailChangeAsync(CancellationToken cancellationToken)
        => RunAsync(
            () => _accountClient.ConfirmEmailAddressAsync(EmailConfirmationCode.Trim(), cancellationToken),
            "Email address confirmed.");

    /// <summary>
    /// Changes the password, then re-wraps the chat key backup under it. Skipping the second half is not
    /// a cosmetic omission: the backup would stay wrapped under the old password, so the next device to
    /// restore it would fail, generate a fresh key, and leave every earlier message unreadable there.
    /// </summary>
    [RelayCommand]
    private async Task ChangePasswordAsync(CancellationToken cancellationToken)
    {
        if (NewPassword != RepeatedNewPassword)
        {
            MessageIsFailure = true;
            Message = _translations["The two new passwords don't match."];
            return;
        }

        var currentPassword = CurrentPassword;
        var newPassword = NewPassword;

        await RunAsync(
            () => _accountClient.ChangePasswordAsync(currentPassword, newPassword, cancellationToken),
            "Password changed.");

        if (MessageIsFailure)
        {
            return;
        }

        CurrentPassword = string.Empty;
        NewPassword = string.Empty;
        RepeatedNewPassword = string.Empty;
        await RewrapChatKeyAsync(currentPassword, newPassword, cancellationToken);
    }

    /// <summary>
    /// Deliberately not fatal: the password has already changed by this point, so a device that could not
    /// re-wrap should say so rather than pretend the change failed. It does have to say so, though - a
    /// silent failure here is exactly what costs someone their history later.
    /// </summary>
    private async Task RewrapChatKeyAsync(string currentPassword, string newPassword, CancellationToken cancellationToken)
    {
        try
        {
            var outcome = await _encryptionKeyProvider.RewrapAsync(currentPassword, newPassword, cancellationToken);
            if (outcome is EncryptionKeyOutcome.StillLocked)
            {
                Message = _translations[
                    "Password changed, but your chat key backup couldn't be updated. "
                    + "Open \"Chat key\" to fix it, or older messages may not open on a new device."];
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            MessageIsFailure = true;
            Message = _translations[
                "Password changed, but your chat key backup couldn't be updated. "
                + "Sign in again while online to fix it."];
            System.Diagnostics.Debug.WriteLine($"Could not re-wrap the chat key backup: {exception}");
        }
    }

    [RelayCommand]
    private void GoToChatKey() => _navigator.ShowChatKeyGate();

    /// <summary>
    /// The way to a new password for somebody asked for one they no longer know - the same screen sign-in
    /// offers. A reset is how an account whose password is gone gets deleted at all.
    /// </summary>
    [RelayCommand]
    private void GoToPasswordReset() => _navigator.ShowPasswordReset();

    /// <summary>
    /// Whether the account has confirmed itself the way it has to before it is deleted, saying on screen
    /// why not when it has not. The page asks this before the platform's own prompt, so a prompt is
    /// never shown for a deletion that would be refused anyway; <see cref="DeleteAccountCommand"/> asks
    /// again, so nothing reaches the server without it.
    ///
    /// An account with a password gives it (see DeleteAccountRequest). One without confirms with Google
    /// where Google is offered (see <see cref="ConfirmsWithGoogle"/>, asked after this agrees); where it is
    /// not, it types its address or login, checked here against the account already loaded, as Orbit.Web
    /// checks it - which makes the press deliberate rather than proving anything to the server.
    /// </summary>
    public bool IsReadyToDelete()
    {
        if (_account is null)
        {
            DeletionMessage = _translations["Your account hasn't loaded yet. Open this screen again and try again."];
            return false;
        }

        if (_account.HasPassword && DeleteAccountPassword.Length == 0)
        {
            DeletionMessage = _translations["Enter your password to confirm."];
            return false;
        }

        if (!_account.HasPassword && !ConfirmsWithGoogle && !ConfirmsAccount(_account))
        {
            DeletionMessage = _translations["That isn't this account's email address or login."];
            return false;
        }

        return true;
    }

    /// <summary>
    /// The address or the login, in any case and with stray spaces ignored: both are what the reader
    /// signs in with, so either is something they know without looking it up. Not a secret - the address
    /// is on this screen - because what this checks is that the deletion was meant.
    /// </summary>
    private bool ConfirmsAccount(Orbit.Contracts.Users.AccountDto account)
    {
        var typed = DeleteAccountConfirmation.Trim();
        return typed.Length > 0
            && (string.Equals(typed, account.Email, StringComparison.OrdinalIgnoreCase)
                || string.Equals(typed, account.UserName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Deletes the account, then leaves this device holding nothing of it.
    ///
    /// The order is the point: the local database is emptied only once the server has agreed. A wrong
    /// password or a lost connection has to leave the phone exactly as it was, because the account it
    /// still belongs to is still there.
    ///
    /// Whether the server agreed is tracked here rather than read back from a message, which a cancelled
    /// request leaves untouched - and "the screen went away mid-request" must never be mistaken for "the
    /// account is gone".
    /// </summary>
    [RelayCommand]
    private async Task DeleteAccountAsync(CancellationToken cancellationToken)
    {
        if (!IsReadyToDelete())
        {
            return;
        }

        await DeleteAsync(googleIdToken: null, cancellationToken);
    }

    /// <summary>
    /// The same deletion, for an account that confirms with Google (<see cref="ConfirmsWithGoogle"/>):
    /// Google is asked again first, and what comes back goes with the request for the server to check.
    /// Backing out of Google's screen deletes nothing and says nothing - it is the reader changing their mind.
    /// </summary>
    [RelayCommand]
    private async Task DeleteWithGoogleAsync(CancellationToken cancellationToken)
    {
        if (!IsReadyToDelete() || !ConfirmsWithGoogle)
        {
            return;
        }

        string? idToken;
        try
        {
            idToken = await GoogleLink.SignInAgainAsync(cancellationToken);
        }
        catch (HttpRequestException)
        {
            DeletionMessage = _translations["Couldn't reach Orbit. Check your connection and try again."];
            return;
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (idToken is null)
        {
            return;
        }

        await DeleteAsync(idToken, cancellationToken);
    }

    private async Task DeleteAsync(string? googleIdToken, CancellationToken cancellationToken)
    {
        var account = _account!;
        DeletionMessage = string.Empty;
        AccountOperationResult result;
        try
        {
            result = await _accountClient.DeleteAccountAsync(DeleteAccountPassword, googleIdToken, cancellationToken);
        }
        catch (HttpRequestException)
        {
            DeletionMessage = _translations["Couldn't reach Orbit. Check your connection and try again."];
            return;
        }
        catch (OperationCanceledException)
        {
            // The screen went away mid-request; there is nobody left to tell.
            return;
        }

        if (!result.Succeeded && googleIdToken is not null)
        {
            // Google's answer, not a password that was set elsewhere: the sign-in was not this account's,
            // or not a fresh one, and trying again means signing in again.
            DeletionMessage = _translations[result.Message ?? "Google didn't confirm this account. Try again."];
            return;
        }

        if (!result.Succeeded)
        {
            await ExplainRefusedDeletionAsync(account, result);
            return;
        }

        DeleteAccountPassword = string.Empty;
        DeleteAccountConfirmation = string.Empty;

        // No sign-out call: it would revoke a refresh token belonging to an account that no longer
        // exists. What is left is all local - the session, the cached database, and what this account
        // was allowed to see. Guid.Empty marks the database as nobody's, as signing out does.
        await _sessionStore.ClearAsync();
        await _localStore.ClearForAsync(Guid.Empty, cancellationToken);
        _permissions.Forget();
        _navigator.ShowSignIn();
    }

    /// <summary>
    /// Refused although no password was asked for means one was set since this screen read the account -
    /// on another device, or at the chat gate. The account is read again so the password field appears,
    /// rather than leaving the reader facing a form that can only be refused the same way again.
    /// </summary>
    private async Task ExplainRefusedDeletionAsync(
        Orbit.Contracts.Users.AccountDto account, AccountOperationResult result)
    {
        if (account.HasPassword || result.Status is not AccountOperationStatus.Refused)
        {
            DeletionMessage = result.Message ?? _translations["That didn't work."];
            return;
        }

        DeletionMessage = _translations["This account has a password now - enter it."];
        await ShowAccountAsync();
    }

    [RelayCommand]
    private void GoToDiagnostics() => _navigator.ShowDiagnostics();

    /// <param name="successMessage">
    /// A dictionary key rather than the text itself, so every caller gets translated without each one
    /// having to remember to ask - see <see cref="Translations"/>.
    /// </param>
    private async Task RunAsync(Func<Task<AccountOperationResult>> operation, string successMessage)
    {
        try
        {
            var result = await operation();
            MessageIsFailure = !result.Succeeded;
            Message = result.Succeeded ? _translations[successMessage] : result.Message ?? _translations["That didn't work."];
        }
        catch (HttpRequestException)
        {
            MessageIsFailure = true;
            Message = _translations["Couldn't reach Orbit. Check your connection and try again."];
        }
        catch (OperationCanceledException)
        {
            // The screen went away mid-request; there is nobody left to tell.
        }
    }

    partial void OnTransferMessageChanged(string value) => OnPropertyChanged(nameof(HasTransferMessage));

    partial void OnTransferWarningChanged(string value) => OnPropertyChanged(nameof(HasTransferWarning));

    partial void OnDeletionMessageChanged(string value) => OnPropertyChanged(nameof(HasDeletionMessage));

    partial void OnIsEmailVerifiedChanged(bool value) => OnPropertyChanged(nameof(EmailVerificationLabel));

    partial void OnTabChanged(AccountTab value)
    {
        OnPropertyChanged(nameof(Tabs));
        OnPropertyChanged(nameof(IsShowingAccount));
        OnPropertyChanged(nameof(IsShowingAppearance));
        OnPropertyChanged(nameof(IsShowingPermissions));
        OnPropertyChanged(nameof(IsShowingDebug));
    }

    /// <summary>Written down and applied at once, the way the theme is.</summary>
    partial void OnAccentChanged(AccentColor value)
    {
        _accents.Write(value);
        OnPropertyChanged(nameof(Accents));
        AccentChanged?.Invoke(this, value);
    }

    /// <inheritdoc cref="ThemeChanged"/>
    public event EventHandler<AccentColor>? AccentChanged;

    /// <summary>Picking one from the strip of three - see Themes.</summary>
    [RelayCommand]
    private void ChooseTheme(ThemeChoice? choice)
    {
        if (choice is not null)
        {
            Theme = choice.Value;
        }
    }

    /// <summary>Picking one from the row of swatches.</summary>
    [RelayCommand]
    private void ChooseAccent(AccentChoice? choice)
    {
        if (choice is not null)
        {
            Accent = choice.Value;
        }
    }

    /// <summary>Written down and applied at once - a theme that took a restart would read as broken.</summary>
    partial void OnThemeChanged(ChosenTheme value)
    {
        _themes.Write(value);
        OnPropertyChanged(nameof(Themes));
        ThemeChanged?.Invoke(this, value);
    }

    /// <summary>
    /// Raised so the platform can apply it. The view model cannot: setting the app's theme is a MAUI
    /// call, and reaching for one here is what would make this screen untestable.
    /// </summary>
    public event EventHandler<ChosenTheme>? ThemeChanged;

    partial void OnPermissionCodeChanged(string value) => RedeemCodeCommand.NotifyCanExecuteChanged();

    partial void OnIsRedeemingCodeChanged(bool value) => RedeemCodeCommand.NotifyCanExecuteChanged();

    partial void OnPermissionMessageChanged(string value) => OnPropertyChanged(nameof(HasPermissionMessage));

    partial void OnMessageChanged(string value) => OnPropertyChanged(nameof(HasMessage));
}
