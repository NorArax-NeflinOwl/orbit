using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Mobile.Authentication;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Sync;

namespace Orbit.Mobile.Screens.Authentication;

/// <summary>
/// Getting back into an account whose password has been forgotten: a code by email, then a new password.
///
/// Reached from the sign-in screen, which is the only place it is any use. The two endpoints behind it
/// have existed all along and both clients already wrapped them, but the only way in was the chat key
/// gate - which is behind signing in. So the one person who needed this could not reach it.
///
/// Two things are deliberate rather than incidental:
///
/// - <b>Asking for a code says nothing about whether the account exists.</b> The server answers a
///   request for an unknown address exactly as it answers a real one (see RequestPasswordResetCommand),
///   and this screen says the same either way. Reporting "no such account" here would turn the sign-in
///   screen into a way of testing whether somebody has an Orbit account.
/// - <b>The new password is typed twice.</b> There is nothing to check it against - the old one is
///   forgotten by definition - so a typo would lock the account a second time, with the code already
///   spent.
/// </summary>
public sealed partial class PasswordResetViewModel : ObservableObject
{
    private readonly AccountClient _accountClient;
    private readonly INetworkStatus _networkStatus;
    private readonly Translations _translations;
    private readonly IScreenNavigator _navigator;

    [ObservableProperty]
    private string _emailOrUserName = string.Empty;

    [ObservableProperty]
    private string _code = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _repeatedPassword = string.Empty;

    [ObservableProperty]
    private string _message = string.Empty;

    /// <summary>
    /// Whether the code has been asked for. The form does not become a second screen for it: the fields
    /// that only make sense afterwards simply appear, so somebody who already has a code from a previous
    /// attempt can still reach them without asking for another.
    /// </summary>
    [ObservableProperty]
    private bool _codeWasRequested;

    /// <summary>
    /// Set once the password has actually been changed. The form goes away rather than staying open
    /// behind a message: the code is spent, so leaving the fields there invites a second attempt that
    /// can only fail.
    /// </summary>
    [ObservableProperty]
    private bool _isDone;

    public PasswordResetViewModel(
        AccountClient accountClient, INetworkStatus networkStatus, Translations translations,
        IScreenNavigator navigator)
    {
        _accountClient = accountClient;
        _networkStatus = networkStatus;
        _translations = translations;
        _navigator = navigator;
    }

    public bool HasMessage => Message.Length > 0;

    /// <summary>
    /// Whether the form is still there to be filled in - the pair to <see cref="IsDone"/>, the way
    /// ConnectionRequirement states both sides of its own question.
    /// </summary>
    public bool IsNotDone => !IsDone;

    /// <summary>Said before anything is typed, since none of this can be queued - see AccountClient.</summary>
    public bool IsOffline => !_networkStatus.IsOnline;

    partial void OnMessageChanged(string value) => OnPropertyChanged(nameof(HasMessage));

    partial void OnIsDoneChanged(bool value) => OnPropertyChanged(nameof(IsNotDone));

    private bool CanSendCode => EmailOrUserName.Trim().Length > 0;

    private bool CanSetPassword
        => CanSendCode && Code.Trim().Length > 0 && Password.Length > 0 && RepeatedPassword.Length > 0;

    partial void OnEmailOrUserNameChanged(string value)
    {
        SendCodeCommand.NotifyCanExecuteChanged();
        SetPasswordCommand.NotifyCanExecuteChanged();
    }

    partial void OnCodeChanged(string value) => SetPasswordCommand.NotifyCanExecuteChanged();

    partial void OnPasswordChanged(string value) => SetPasswordCommand.NotifyCanExecuteChanged();

    partial void OnRepeatedPasswordChanged(string value) => SetPasswordCommand.NotifyCanExecuteChanged();

    [RelayCommand(CanExecute = nameof(CanSendCode), AllowConcurrentExecutions = false)]
    private async Task SendCodeAsync(CancellationToken cancellationToken)
    {
        AccountOperationResult result;
        try
        {
            result = await _accountClient.RequestPasswordResetAsync(EmailOrUserName.Trim(), cancellationToken);
        }
        catch (HttpRequestException)
        {
            Message = _translations["Couldn't reach Orbit. Check your connection and try again."];
            return;
        }

        if (!result.Succeeded)
        {
            Message = result.Message ?? _translations["Couldn't send a reset code."];
            return;
        }

        CodeWasRequested = true;

        // Said in the conditional, whatever the account turns out to be. See the note on this class.
        Message = _translations["If that account exists, a code is on its way to the address it was registered with."];
    }

    [RelayCommand(CanExecute = nameof(CanSetPassword), AllowConcurrentExecutions = false)]
    private async Task SetPasswordAsync(CancellationToken cancellationToken)
    {
        if (Password != RepeatedPassword)
        {
            Message = _translations["The two new passwords don't match."];
            return;
        }

        AccountOperationResult result;
        try
        {
            result = await _accountClient.ResetPasswordAsync(
                EmailOrUserName.Trim(), Code.Trim(), Password, cancellationToken);
        }
        catch (HttpRequestException)
        {
            Message = _translations["Couldn't reach Orbit. Check your connection and try again."];
            return;
        }

        if (!result.Succeeded)
        {
            Message = result.Message ?? _translations["That code isn't valid any more. Request a new one."];
            return;
        }

        // Nothing is unlocked or signed in here on purpose: the chat key is wrapped with the password
        // that is now gone, and what replaces it is decided at the gate, where the warning about losing
        // the messages sealed under the old one is - see ChatKeyGateViewModel.
        Password = string.Empty;
        RepeatedPassword = string.Empty;
        Code = string.Empty;
        IsDone = true;
        Message = _translations["Password changed. Sign in with the new one."];
    }

    [RelayCommand]
    private void GoToSignIn() => _navigator.ShowSignIn();
}
