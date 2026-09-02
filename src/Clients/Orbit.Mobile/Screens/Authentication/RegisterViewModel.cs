using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Mobile.Authentication;
using Orbit.Mobile.Crypto;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Sync;

namespace Orbit.Mobile.Screens.Authentication;

/// <summary>
/// Creating an account. Online only, and deliberately so - see <see cref="AccountClient"/>. The account
/// is created on the server first; nothing is kept on this phone until that has succeeded, so a local
/// account the server has never heard of cannot exist.
/// </summary>
public sealed partial class RegisterViewModel : ObservableObject
{
    private readonly AccountClient _accountClient;
    private readonly SignInCompletion _completion;
    private readonly INetworkStatus _networkStatus;
    private readonly Translations _translations;
    private readonly IScreenNavigator _navigator;

    [ObservableProperty]
    private string _emailAddress = string.Empty;

    [ObservableProperty]
    private string _userName = string.Empty;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    /// <summary>
    /// Typed a second time, because a password nobody can see is a password nobody can check - and one
    /// mistyped here locks somebody out of an account they have just made. Orbit.Web has asked for it
    /// since it had a form; this screen took the first answer and made the account from it.
    /// </summary>
    [ObservableProperty]
    private string _repeatedPassword = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public RegisterViewModel(
        AccountClient accountClient, SignInCompletion completion, INetworkStatus networkStatus,
        Translations translations, IScreenNavigator navigator)
    {
        _accountClient = accountClient;
        _completion = completion;
        _networkStatus = networkStatus;
        _translations = translations;
        _navigator = navigator;
    }

    public bool HasError => ErrorMessage.Length > 0;

    /// <summary>
    /// Shown before anything is typed, so someone offline finds out now rather than after filling in
    /// four fields.
    /// </summary>
    public bool IsOffline => !_networkStatus.IsOnline;

    private bool CanRegister
        => EmailAddress.Trim().Length > 0 && UserName.Trim().Length > 0
            && DisplayName.Trim().Length > 0 && Password.Length > 0 && RepeatedPassword.Length > 0;

    [RelayCommand(CanExecute = nameof(CanRegister), AllowConcurrentExecutions = false)]
    private async Task RegisterAsync(CancellationToken cancellationToken)
    {
        ErrorMessage = string.Empty;

        // Checked here rather than only on the button, so the reason is said out loud: a button that
        // stays dead tells nobody which of the two fields is wrong.
        if (Password != RepeatedPassword)
        {
            ErrorMessage = _translations["The two passwords don't match."];
            return;
        }

        AccountOperationResult result;
        try
        {
            result = await _accountClient.RegisterAsync(
                EmailAddress.Trim(), UserName.Trim(), DisplayName.Trim(), Password, cancellationToken);
        }
        catch (HttpRequestException)
        {
            ErrorMessage = _translations["Couldn't reach Orbit. Check your connection and try again."];
            return;
        }

        if (!result.Succeeded)
        {
            ErrorMessage = result.Message ?? _translations["Couldn't create that account."];
            return;
        }

        // The same steps a sign-in takes, and for the same reasons - see SignInCompletion. A brand-new
        // account has no key backup to restore, so the unlock in there generates one and publishes it
        // while the password is still on hand, which is the only moment it can be wrapped at all.
        await _completion.CompleteAsync(Password, cancellationToken);

        Password = string.Empty;
        RepeatedPassword = string.Empty;
        _navigator.ShowDashboard();
    }

    [RelayCommand]
    private void GoToSignIn() => _navigator.ShowSignIn();

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));

    partial void OnEmailAddressChanged(string value) => RegisterCommand.NotifyCanExecuteChanged();

    partial void OnUserNameChanged(string value) => RegisterCommand.NotifyCanExecuteChanged();

    partial void OnDisplayNameChanged(string value) => RegisterCommand.NotifyCanExecuteChanged();

    partial void OnPasswordChanged(string value) => RegisterCommand.NotifyCanExecuteChanged();

    partial void OnRepeatedPasswordChanged(string value) => RegisterCommand.NotifyCanExecuteChanged();
}
