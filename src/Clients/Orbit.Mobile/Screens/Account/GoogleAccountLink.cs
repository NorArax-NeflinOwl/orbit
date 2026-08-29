using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Contracts.Users;
using Orbit.Mobile.Authentication;
using Orbit.Mobile.Localization;

namespace Orbit.Mobile.Screens.Account;

/// <summary>
/// The Google account connected to this one, and connecting or disconnecting it - Orbit.Web's "Google"
/// row on the options page. Its own object rather than more members on the account screen, for the same
/// reason SharePanel is one: a small feature with state, two commands and a message line of its own.
///
/// Everything here needs a connection - a Google account is not something a phone can decide it holds.
/// </summary>
public sealed partial class GoogleAccountLink : ObservableObject
{
    private readonly AccountClient _accountClient;
    private readonly AuthenticationClient _authenticationClient;
    private readonly GoogleSignIn _googleSignIn;
    private readonly Translations _translations;

    public GoogleAccountLink(
        AccountClient accountClient, AuthenticationClient authenticationClient, GoogleSignIn googleSignIn,
        Translations translations)
    {
        _accountClient = accountClient;
        _authenticationClient = authenticationClient;
        _googleSignIn = googleSignIn;
        _translations = translations;
    }

    /// <summary>
    /// Whether this deployment has a Google client id for this app at all. The row is absent rather
    /// than disabled without one, as the sign-in screen's button is: there is nothing to explain.
    /// </summary>
    [ObservableProperty]
    private bool _isOffered;

    [ObservableProperty]
    private bool _isLinked;

    /// <summary>
    /// Whether the account has a password to fall back on. Disconnecting without one is refused by the
    /// server, so the screen says so before the reader finds out by being told no.
    /// </summary>
    [ObservableProperty]
    private bool _hasPassword;

    [ObservableProperty]
    private string _message = string.Empty;

    public bool HasMessage => Message.Length > 0;

    public bool CanConnect => IsOffered && !IsLinked;

    public bool IsOnlyWayIn => IsLinked && !HasPassword;

    /// <summary>Raised when the link changed, so the account screen reads the account again.</summary>
    public event EventHandler? Changed;

    public async Task ShowAsync(AccountDto account, CancellationToken cancellationToken = default)
    {
        IsLinked = account.IsGoogleLinked;
        HasPassword = account.HasPassword;
        Message = string.Empty;

        try
        {
            IsOffered = (await _authenticationClient.GoogleClientIdAsync(
                _googleSignIn.Platform, cancellationToken)).Length > 0;
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            // Not offered rather than offered-and-broken: a button that cannot work is worse than none.
            IsOffered = false;
        }
    }

    /// <summary>
    /// Sends the reader to Google and hands the server what comes back - see <see cref="GoogleSignIn"/>.
    /// Connecting is the same journey as signing in with Google; only what Orbit does with the token
    /// afterwards differs.
    /// </summary>
    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task ConnectAsync(CancellationToken cancellationToken)
    {
        Message = string.Empty;

        try
        {
            var clientId = await _authenticationClient.GoogleClientIdAsync(
                _googleSignIn.Platform, cancellationToken);

            if (await _googleSignIn.GetIdTokenAsync(clientId, cancellationToken) is not { } idToken)
            {
                // Backing out of Google's screen is a choice, and reporting the reader's own decision
                // back to them as a problem would be wrong - the sign-in screen says nothing either.
                return;
            }

            Say(await _accountClient.LinkGoogleAsync(idToken, cancellationToken),
                _translations["Google connected."]);
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            Message = _translations["Couldn't connect Google. Try again."];
        }
    }

    /// <summary>
    /// Refused by the server while Google is the only way in, which is the case worth getting right:
    /// disconnecting then would leave an account nobody could sign in to.
    /// </summary>
    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task DisconnectAsync(CancellationToken cancellationToken)
    {
        Message = string.Empty;

        try
        {
            Say(await _accountClient.UnlinkGoogleAsync(cancellationToken),
                _translations["Google disconnected."]);
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            Message = _translations["Couldn't disconnect Google. Try again."];
        }
    }

    /// <summary>
    /// What the server said, in the reader's language. A refusal carries its own reason - "already
    /// connected to a different Orbit account", "set a password first" - and repeating it is more use
    /// than replacing it with a general failure.
    /// </summary>
    private void Say(AccountOperationResult outcome, string success)
    {
        Message = outcome.Succeeded ? success : _translations[outcome.Message ?? "That didn't work."];
        if (outcome.Succeeded)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    partial void OnMessageChanged(string value) => OnPropertyChanged(nameof(HasMessage));

    partial void OnIsLinkedChanged(bool value)
    {
        OnPropertyChanged(nameof(CanConnect));
        OnPropertyChanged(nameof(IsOnlyWayIn));
    }

    partial void OnIsOfferedChanged(bool value) => OnPropertyChanged(nameof(CanConnect));

    partial void OnHasPasswordChanged(bool value) => OnPropertyChanged(nameof(IsOnlyWayIn));
}
