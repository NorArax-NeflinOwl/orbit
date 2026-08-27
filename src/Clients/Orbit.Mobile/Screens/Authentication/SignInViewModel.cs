using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Mobile.Authentication;
using Orbit.Mobile.Crypto;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Notifications;

namespace Orbit.Mobile.Screens.Authentication;

public sealed partial class SignInViewModel : ObservableObject
{
    private readonly AuthenticationClient _authenticationClient;
    private readonly OwnEncryptionKeyProvider _encryptionKeyProvider;
    private readonly PushRegistration _pushRegistration;
    private readonly SessionStore _sessionStore;
    private readonly LocalStoreReset _localStore;
    private readonly Translations _translations;
    private readonly IScreenNavigator _navigator;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SignInCommand))]
    private string _emailOrUserName = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SignInCommand))]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public SignInViewModel(
        AuthenticationClient authenticationClient, OwnEncryptionKeyProvider encryptionKeyProvider,
        PushRegistration pushRegistration, SessionStore sessionStore, LocalStoreReset localStore,
        Translations translations, IScreenNavigator navigator)
    {
        _authenticationClient = authenticationClient;
        _encryptionKeyProvider = encryptionKeyProvider;
        _pushRegistration = pushRegistration;
        _sessionStore = sessionStore;
        _localStore = localStore;
        _translations = translations;
        _navigator = navigator;
    }

    public bool HasError => ErrorMessage.Length > 0;

    private bool CanSignIn => EmailOrUserName.Length > 0 && Password.Length > 0 && !SignInCommand.IsRunning;

    [RelayCommand(CanExecute = nameof(CanSignIn), AllowConcurrentExecutions = false)]
    private async Task SignInAsync(CancellationToken cancellationToken)
    {
        ErrorMessage = string.Empty;

        try
        {
            var result = await _authenticationClient.SignInAsync(EmailOrUserName, Password, cancellationToken);
            if (!result.Succeeded)
            {
                // A refusal and a missing connection say different things on purpose: sending someone to
                // reset a password that was fine, because their train went into a tunnel, is a bad way
                // to lose an account.
                ErrorMessage = result.Message ?? _translations["Those details weren't recognised."];
                return;
            }
        }
        catch (HttpRequestException)
        {
            // Connectivity said yes and the request still failed - a captive portal, most likely.
            ErrorMessage = _translations["Couldn't reach Orbit. Check your connection and try again."];
            return;
        }

        // Before anything is read from the local store: on a phone that somebody else was signed
        // into, everything cached - notes, the calendar, decrypted messages - is still there, and the
        // sign-in screen is reached without a sign-out whenever a session simply expires.
        if (await _sessionStore.GetAsync() is { } session)
        {
            await _localStore.ClearIfSomebodyElsesAsync(session.UserId, cancellationToken);
        }

        // The one moment the plaintext password exists - see OwnEncryptionKeyProvider. Best-effort:
        // failing here leaves chat locked, which the user can recover from, and must not block sign-in.
        await TryUnlockChatKeyAsync(Password, cancellationToken);

        // Every sign-in, not just the first: a push token changes when the app is reinstalled or its
        // data cleared, and the old one stops working without saying so. Best-effort like the key
        // unlock above - push is an addition to the in-app feed, never a reason to fail a sign-in.
        await _pushRegistration.RegisterThisDeviceAsync(cancellationToken);

        Password = string.Empty;
        _navigator.ShowDashboard();
    }

    private async Task TryUnlockChatKeyAsync(string password, CancellationToken cancellationToken)
    {
        try
        {
            await _encryptionKeyProvider.UnlockOrCreateAsync(password, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine($"Could not unlock the chat key after signing in: {exception}");
        }
    }

    [RelayCommand]
    private void GoToRegister() => _navigator.ShowRegister();

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));
}
