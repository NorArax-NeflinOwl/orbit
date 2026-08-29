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
    private readonly GoogleSignIn _googleSignIn;
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

    /// <summary>
    /// Whether to show the Google button at all. False until the server has been asked, so a deployment
    /// without Google configured - and a phone that could not reach one - shows no button rather than
    /// one that leads nowhere.
    /// </summary>
    [ObservableProperty]
    private bool _isGoogleOffered;

    public SignInViewModel(
        AuthenticationClient authenticationClient, GoogleSignIn googleSignIn, OwnEncryptionKeyProvider encryptionKeyProvider,
        PushRegistration pushRegistration, SessionStore sessionStore, LocalStoreReset localStore,
        Translations translations, IScreenNavigator navigator)
    {
        _authenticationClient = authenticationClient;
        _googleSignIn = googleSignIn;
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

        await FinishSignInAsync(Password, cancellationToken);
    }

    /// <summary>
    /// Signing in with Google, which is also registering: whether the identity is new to Orbit is the
    /// server's business - see AuthEndpoints. Offered only where the deployment has a client id for this
    /// app, so <see cref="IsGoogleOffered"/> decides whether there is a button at all.
    /// </summary>
    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task SignInWithGoogleAsync(CancellationToken cancellationToken)
    {
        ErrorMessage = string.Empty;

        try
        {
            var clientId = await _authenticationClient.GoogleClientIdAsync(_googleSignIn.Platform, cancellationToken);
            if (await _googleSignIn.GetIdTokenAsync(clientId, cancellationToken) is not { } idToken)
            {
                // Backing out of Google's screen is a choice, and saying something about it would be
                // reporting the reader's own decision back at them as a problem.
                return;
            }

            var result = await _authenticationClient.SignInWithGoogleAsync(idToken, cancellationToken);
            if (!result.Succeeded)
            {
                ErrorMessage = result.Message ?? _translations["Google couldn't sign you in to Orbit."];
                return;
            }
        }
        catch (HttpRequestException)
        {
            ErrorMessage = _translations["Couldn't reach Orbit. Check your connection and try again."];
            return;
        }

        // No password to unlock the chat key with, deliberately: there is none to have. Chat stays
        // locked until the reader opens it and the key gate asks - see ChatKeyGateViewModel, which is
        // the same path a password sign-in takes when its unlock does not work.
        await FinishSignInAsync(password: null, cancellationToken);
    }

    /// <summary>Whether this deployment has a Google client id for this app, learned on first showing.</summary>
    [RelayCommand]
    private async Task LoadAsync(CancellationToken cancellationToken)
        => IsGoogleOffered =
            (await _authenticationClient.GoogleClientIdAsync(_googleSignIn.Platform, cancellationToken)).Length > 0;

    /// <summary>
    /// Everything the two ways in share, in the order that matters. The store is cleared before anything
    /// reads it, and the two best-effort steps come after: neither is a reason to fail a sign-in that
    /// the server has already accepted.
    /// </summary>
    /// <param name="password">
    /// Null for a Google sign-in, which has none. Only a plaintext password can open the chat key - see
    /// OwnEncryptionKeyProvider - so without one the key stays locked.
    /// </param>
    private async Task FinishSignInAsync(string? password, CancellationToken cancellationToken)
    {
        // Before anything is read from the local store: on a phone that somebody else was signed
        // into, everything cached - notes, the calendar, decrypted messages - is still there, and the
        // sign-in screen is reached without a sign-out whenever a session simply expires.
        if (await _sessionStore.GetAsync() is { } session)
        {
            await _localStore.ClearIfSomebodyElsesAsync(session.UserId, cancellationToken);
        }

        // The one moment the plaintext password exists - see OwnEncryptionKeyProvider. Best-effort:
        // failing here leaves chat locked, which the user can recover from, and must not block sign-in.
        if (password is { Length: > 0 })
        {
            await TryUnlockChatKeyAsync(password, cancellationToken);
        }

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
