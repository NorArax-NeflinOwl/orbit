using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Mobile.Authentication;
using Orbit.Mobile.Crypto;
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
    private readonly OwnEncryptionKeyProvider _encryptionKeyProvider;
    private readonly INetworkStatus _networkStatus;
    private readonly IScreenNavigator _navigator;

    [ObservableProperty]
    private string _emailAddress = string.Empty;

    [ObservableProperty]
    private string _userName = string.Empty;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public RegisterViewModel(
        AccountClient accountClient, OwnEncryptionKeyProvider encryptionKeyProvider,
        INetworkStatus networkStatus, IScreenNavigator navigator)
    {
        _accountClient = accountClient;
        _encryptionKeyProvider = encryptionKeyProvider;
        _networkStatus = networkStatus;
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
            && DisplayName.Trim().Length > 0 && Password.Length > 0;

    [RelayCommand(CanExecute = nameof(CanRegister), AllowConcurrentExecutions = false)]
    private async Task RegisterAsync(CancellationToken cancellationToken)
    {
        ErrorMessage = string.Empty;

        AccountOperationResult result;
        try
        {
            result = await _accountClient.RegisterAsync(
                EmailAddress.Trim(), UserName.Trim(), DisplayName.Trim(), Password, cancellationToken);
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "Couldn't reach Orbit. Check your connection and try again.";
            return;
        }

        if (!result.Succeeded)
        {
            ErrorMessage = result.Message ?? "Couldn't create that account.";
            return;
        }

        // A brand-new account has no backup to restore, so this generates the key and publishes it while
        // the password is still on hand - the only time it can be wrapped at all.
        try
        {
            await _encryptionKeyProvider.UnlockOrCreateAsync(Password, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine($"Could not create the chat key after registering: {exception}");
        }

        Password = string.Empty;
        _navigator.ShowNotes();
    }

    [RelayCommand]
    private void GoToSignIn() => _navigator.ShowSignIn();

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));

    partial void OnEmailAddressChanged(string value) => RegisterCommand.NotifyCanExecuteChanged();

    partial void OnUserNameChanged(string value) => RegisterCommand.NotifyCanExecuteChanged();

    partial void OnDisplayNameChanged(string value) => RegisterCommand.NotifyCanExecuteChanged();

    partial void OnPasswordChanged(string value) => RegisterCommand.NotifyCanExecuteChanged();
}
