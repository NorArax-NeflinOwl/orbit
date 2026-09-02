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
    /// Confirms the deletion below. Kept apart from <see cref="CurrentPassword"/> deliberately: typing a
    /// password into the change-password box and then pressing a delete button that silently reused it
    /// is the one mistake this screen must not make possible.
    /// </summary>
    [ObservableProperty]
    private string _deleteAccountPassword = string.Empty;

    /// <summary>The address the account signs in with today, which the form below changes.</summary>
    [ObservableProperty]
    private string _emailAddress = string.Empty;

    [ObservableProperty]
    private bool _isEmailVerified;

    /// <summary>
    /// Whether deleting needs the password. False for a Google account that never set one - being signed
    /// in is the proof there, and DeleteAccountCommandHandler says so on the server. True until the
    /// account has actually been read: asking for a password that turns out not to be needed is a
    /// nuisance, while not asking when it is needed looks like the deletion silently failed.
    /// </summary>
    [ObservableProperty]
    private bool _requiresPasswordToDelete = true;

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
        Export.PropertyChanged += (_, _) => OnPropertyChanged(nameof(CanExport));
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

    public IReadOnlyList<AccountTabRow> Tabs
        => [.. Enum.GetValues<AccountTab>()
            .Select(tab => new AccountTabRow(tab, AccountTabRow.Describe(tab, _translations), tab == Tab))];

    public bool IsShowingAccount => Tab is AccountTab.Account;

    public bool IsShowingAppearance => Tab is AccountTab.Appearance;

    public bool IsShowingPermissions => Tab is AccountTab.Permissions;

    public bool IsShowingDebug => Tab is AccountTab.Debug;

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

    /// <summary>What the file picker is titled - the picker is the platform's, the words are the app's.</summary>
    public string ImportPickerTitle => _translations["Import"];

    /// <summary>
    /// Raised with the file's name and its contents once an export is built. Writing it and handing it
    /// somewhere is a platform call - the share sheet - and reaching for one here is what would make
    /// this screen untestable.
    /// </summary>
    public event EventHandler<(string FileName, string Json)>? ExportReady;

    /// <summary>
    /// What the next export will carry - all four parts unless the reader says otherwise, the same four
    /// the browser offers. See ExportChoice.
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
        try
        {
            var everything = await _transfer.ExportAsync(cancellationToken);
            if (everything is null)
            {
                TransferMessage = _translations["Couldn't build the export. Try again."];
                return;
            }

            var archive = Export.Narrow(everything);
            // Said rather than left to the file: what was asked for and what came back are two different
            // things, and a file nobody opens is where that difference would otherwise be found.
            TransferMessage = _translations.Format(
                "Exported {0} notes, {1} task lists, {2} events and {3} storages.",
                archive.Notes.Count, archive.TaskLists.Count, archive.CalendarEvents.Count,
                archive.Warehouses.Count);

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
                    "Imported {0} notes, {1} task lists, {2} events and {3} storages.",
                    result.Notes, result.TaskLists, result.CalendarEvents, result.Warehouses);
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

    public IReadOnlyList<ThemeChoice> Themes
        => [.. Enum.GetValues<ChosenTheme>()
            .Select(theme => new ThemeChoice(theme, ThemeChoice.Describe(theme, _translations)))];

    /// <summary>What the picker has selected. Its own property because a picker names objects, not enums.</summary>
    public ThemeChoice ChosenThemeOption
    {
        get => Themes.Single(choice => choice.Value == Theme);
        set => Theme = value.Value;
    }

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

            UserName = account.UserName;
            DisplayName = account.DisplayName;
            EmailAddress = account.Email;
            IsEmailVerified = account.IsEmailVerified;
            RequiresPasswordToDelete = account.HasPassword;
            // A switch for something the account cannot use yet would turn nothing off, so it is only
            // offered where the account qualifies - the line Orbit.Web draws over the same row.
            CanChooseGoogleExtras = GoogleIntegrationAccess.Qualifies(account);
            await GoogleLink.ShowAsync(account);
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
            Permissions.Add(PermissionRow.For(permission, granted, _translations));
        }
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
    /// Deletes the account, then leaves this device holding nothing of it.
    ///
    /// The order is the point: the local database is emptied only once the server has agreed. A wrong
    /// password or a lost connection has to leave the phone exactly as it was, because the account it
    /// still belongs to is still there.
    ///
    /// Whether the server agreed is tracked here rather than read back from <see cref="MessageIsFailure"/>,
    /// which a cancelled request leaves untouched - and "the screen went away mid-request" must never be
    /// mistaken for "the account is gone".
    /// </summary>
    [RelayCommand]
    private async Task DeleteAccountAsync(CancellationToken cancellationToken)
    {
        var deleted = false;

        await RunAsync(
            async () =>
            {
                var result = await _accountClient.DeleteAccountAsync(DeleteAccountPassword, cancellationToken);
                deleted = result.Succeeded;
                return result;
            },
            "Your account has been deleted.");

        if (!deleted)
        {
            return;
        }

        DeleteAccountPassword = string.Empty;

        // No sign-out call: it would revoke a refresh token belonging to an account that no longer
        // exists. What is left is all local - the session, the cached database, and what this account
        // was allowed to see. Guid.Empty marks the database as nobody's, as signing out does.
        await _sessionStore.ClearAsync();
        await _localStore.ClearForAsync(Guid.Empty, cancellationToken);
        _permissions.Forget();
        _navigator.ShowSignIn();
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
        OnPropertyChanged(nameof(ChosenThemeOption));
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
