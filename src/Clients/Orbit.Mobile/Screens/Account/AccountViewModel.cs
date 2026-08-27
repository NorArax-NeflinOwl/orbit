using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Mobile.Authentication;
using Orbit.Mobile.Crypto;
using System.Collections.ObjectModel;
using Orbit.Core.Permissions;
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
    private readonly OwnEncryptionKeyProvider _encryptionKeyProvider;
    private readonly INetworkStatus _networkStatus;
    private readonly SessionStore _sessionStore;
    private readonly Translations _translations;
    private readonly UsersClient _usersClient;
    private readonly UserPermissions _permissions;
    private readonly IThemeStore _themes;
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
        AccountClient accountClient, OwnEncryptionKeyProvider encryptionKeyProvider, INetworkStatus networkStatus,
        SessionStore sessionStore, Translations translations, UsersClient usersClient,
        UserPermissions permissions, IThemeStore themes, IScreenNavigator navigator)
    {
        _accountClient = accountClient;
        _encryptionKeyProvider = encryptionKeyProvider;
        _networkStatus = networkStatus;
        _sessionStore = sessionStore;
        _translations = translations;
        _usersClient = usersClient;
        _permissions = permissions;
        _themes = themes;
        _theme = themes.Read();
        _navigator = navigator;
    }

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

    /// <summary>What this account may use, and what it would take to unlock the rest.</summary>
    public ObservableCollection<PermissionRow> Permissions { get; } = [];

    public bool HasPermissionMessage => PermissionMessage.Length > 0;

    /// <summary>Everything on this screen is unavailable offline, so the whole form reflects one flag.</summary>
    public bool IsOnline => _networkStatus.IsOnline;

    public bool IsOffline => !IsOnline;

    public bool HasMessage => Message.Length > 0;

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (await _sessionStore.GetAsync() is { } session)
        {
            DisplayName = session.DisplayName;
        }

        OnPropertyChanged(nameof(IsOnline));
        OnPropertyChanged(nameof(IsOffline));

        await _permissions.EnsureLoadedAsync();
        ShowPermissions();
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

    private bool CanRedeemCode => PermissionCode.Trim().Length > 0 && !IsRedeemingCode && IsOnline;

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

    [RelayCommand]
    private void GoToDiagnostics() => _navigator.ShowDiagnostics();

    [RelayCommand]
    private void GoBack() => _navigator.ShowDashboard();

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

    partial void OnTabChanged(AccountTab value)
    {
        OnPropertyChanged(nameof(Tabs));
        OnPropertyChanged(nameof(IsShowingAccount));
        OnPropertyChanged(nameof(IsShowingAppearance));
        OnPropertyChanged(nameof(IsShowingPermissions));
        OnPropertyChanged(nameof(IsShowingDebug));
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
