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
    private readonly SignInCompletion _completion;
    private readonly NotificationOpener _notificationOpener;
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
        AuthenticationClient authenticationClient, GoogleSignIn googleSignIn, SignInCompletion completion,
        NotificationOpener notificationOpener, Translations translations, IScreenNavigator navigator)
    {
        _authenticationClient = authenticationClient;
        _googleSignIn = googleSignIn;
        _completion = completion;
        _notificationOpener = notificationOpener;
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
    /// Null for a Google sign-in, which has no password - see <see cref="SignInCompletion"/> for what
    /// that costs and why it is not a failure.
    /// </summary>
    private async Task FinishSignInAsync(string? password, CancellationToken cancellationToken)
    {
        await _completion.CompleteAsync(password, cancellationToken);
        Password = string.Empty;

        // What they were trying to reach when the app asked them to sign in first - a tapped
        // notification, or a link Android handed to Orbit. Held rather than followed until now, because
        // there was no account to open it in; taken here rather than only at a cold start, which is the
        // one place that used to look - see PendingNotificationTap and StartupViewModel.
        if (await _notificationOpener.FollowTapThatLaunchedTheAppAsync(cancellationToken))
        {
            return;
        }

        _navigator.ShowDashboard();
    }

    [RelayCommand]
    private void GoToRegister() => _navigator.ShowRegister();

    /// <summary>
    /// For somebody who cannot get past this screen at all - see PasswordResetViewModel. Until it was
    /// offered here, the reset flow existed but was reachable only from behind signing in.
    /// </summary>
    [RelayCommand]
    private void GoToPasswordReset() => _navigator.ShowPasswordReset();

    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));
}
