using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Contracts.Users;
using Orbit.Mobile.Authentication;
using Orbit.Mobile.Crypto;

namespace Orbit.Mobile.Screens.Chat;

/// <summary>Which of the gate's situations the user is in.</summary>
public enum ChatKeyGateMode
{
    Loading,

    /// <summary>A Google account that never set a password. There is nothing to wrap the key with yet.</summary>
    SetFirstPassword,

    /// <summary>The account has a password; this device just doesn't have the key yet.</summary>
    EnterPassword,

    /// <summary>The password is gone, so the backup can never be opened again - see the warning.</summary>
    Reset,

    Unlocked
}

/// <summary>
/// Stands between the user and chat when this device has no usable encryption key. The mobile
/// counterpart of Orbit.Web's ChatPasswordGate, and kept in one place for the same reason: the three
/// situations differ only in which secret unlocks the key.
///
/// The reset path is where this diverges from the web client, and it has to. Orbit.Web generates a fresh
/// key whenever a backup will not open, so a reset happens to work; the mobile provider refuses that by
/// default, because on a phone it would otherwise fire on a lost connection and destroy a key nobody
/// asked it to touch. So a reset here calls
/// <see cref="OwnEncryptionKeyProvider.ReplaceAfterPasswordResetAsync"/> explicitly - the user has just
/// been told, in as many words, that their existing messages become unreadable, and chose it anyway.
/// </summary>
public sealed partial class ChatKeyGateViewModel : ObservableObject
{
    private readonly AccountClient _accountClient;
    private readonly OwnEncryptionKeyProvider _encryptionKeyProvider;
    private readonly IScreenNavigator _navigator;

    private AccountDto? _account;

    [ObservableProperty]
    private ChatKeyGateMode _mode = ChatKeyGateMode.Loading;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _repeatPassword = string.Empty;

    [ObservableProperty]
    private string _code = string.Empty;

    [ObservableProperty]
    private bool _codeSent;

    [ObservableProperty]
    private string _message = string.Empty;

    public ChatKeyGateViewModel(
        AccountClient accountClient, OwnEncryptionKeyProvider encryptionKeyProvider, IScreenNavigator navigator)
    {
        _accountClient = accountClient;
        _encryptionKeyProvider = encryptionKeyProvider;
        _navigator = navigator;
    }

    public bool IsSettingFirstPassword => Mode is ChatKeyGateMode.SetFirstPassword;

    public bool IsEnteringPassword => Mode is ChatKeyGateMode.EnterPassword;

    public bool IsResetting => Mode is ChatKeyGateMode.Reset;

    public bool IsUnlocked => Mode is ChatKeyGateMode.Unlocked;

    public bool HasMessage => Message.Length > 0;

    /// <summary>Resetting sends a code by email, so an unconfirmed address has nowhere to send it.</summary>
    public bool CanReset => ChatKeyGate.CanResetPassword(_account?.IsEmailVerified is true);

    /// <summary>The code hasn't been asked for yet, so the form for entering one isn't useful.</summary>
    public bool AwaitingCodeRequest => !CodeSent;

    public string EmailAddress => _account?.Email ?? string.Empty;

    [RelayCommand]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            var deviceHoldsTheKey = await _encryptionKeyProvider.HasKeyAsync(cancellationToken);
            if (!deviceHoldsTheKey)
            {
                _account = await _accountClient.GetAccountAsync(cancellationToken);
            }

            SetMode(ToMode(ChatKeyGate.Decide(deviceHoldsTheKey, _account?.HasPassword is true)));
        }
        catch (Exception exception) when (exception is HttpRequestException or EncryptionKeyLockedException)
        {
            SetMode(ChatKeyGateMode.EnterPassword);
            Message = "Couldn't load your account. Check your connection and try again.";
        }
    }

    /// <summary>The Google-account case: no current password to prove, so this only sets one and unlocks.</summary>
    [RelayCommand]
    private async Task SetFirstPasswordAsync(CancellationToken cancellationToken)
    {
        if (!PasswordsMatch())
        {
            return;
        }

        var result = await _accountClient.SetFirstPasswordAsync(Password, cancellationToken);
        if (!result.Succeeded)
        {
            Message = result.Message ?? "Couldn't set that password.";
            return;
        }

        await UnlockWithCurrentPasswordAsync(cancellationToken);
    }

    [RelayCommand]
    private Task UnlockAsync(CancellationToken cancellationToken) => UnlockWithCurrentPasswordAsync(cancellationToken);

    [RelayCommand]
    private async Task SendResetCodeAsync(CancellationToken cancellationToken)
    {
        var result = await _accountClient.RequestPasswordResetAsync(EmailAddress, cancellationToken);
        if (!result.Succeeded)
        {
            Message = result.Message ?? "Couldn't send a reset code.";
            return;
        }

        CodeSent = true;
        Message = string.Empty;
    }

    /// <summary>
    /// After this the old backup can never be opened by anyone, so the key is replaced deliberately
    /// rather than left locked - which is exactly what the warning on this screen is about.
    /// </summary>
    [RelayCommand]
    private async Task ResetPasswordAsync(CancellationToken cancellationToken)
    {
        if (!PasswordsMatch())
        {
            return;
        }

        var result = await _accountClient.ResetPasswordAsync(EmailAddress, Code, Password, cancellationToken);
        if (!result.Succeeded)
        {
            Message = result.Message ?? "That code isn't valid any more. Request a new one.";
            return;
        }

        await _encryptionKeyProvider.ReplaceAfterPasswordResetAsync(Password, cancellationToken);
        SetMode(ChatKeyGateMode.Unlocked);
    }

    [RelayCommand]
    private void StartReset() => SetMode(ChatKeyGateMode.Reset);

    [RelayCommand]
    private void CancelReset()
        => SetMode(ToMode(ChatKeyGate.Decide(deviceHoldsTheKey: false, _account?.HasPassword is true)));

    private static ChatKeyGateMode ToMode(ChatKeyGateSituation situation) => situation switch
    {
        ChatKeyGateSituation.AlreadyUnlocked => ChatKeyGateMode.Unlocked,
        ChatKeyGateSituation.SetFirstPassword => ChatKeyGateMode.SetFirstPassword,
        _ => ChatKeyGateMode.EnterPassword
    };

    [RelayCommand]
    private void GoBack() => _navigator.ShowNotes();

    private async Task UnlockWithCurrentPasswordAsync(CancellationToken cancellationToken)
    {
        try
        {
            var outcome = await _encryptionKeyProvider.UnlockOrCreateAsync(Password, cancellationToken);
            if (outcome is EncryptionKeyOutcome.StillLocked)
            {
                // Deliberately not "wrong password": it is also what a lost connection looks like, and
                // the app will not replace a key it could not check.
                Message = "Couldn't unlock your chat key. Either that isn't the password it was saved " +
                    "under, or Orbit couldn't be reached. Nothing was changed.";
                return;
            }

            SetMode(ChatKeyGateMode.Unlocked);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Message = "Something went wrong. Try again.";
            System.Diagnostics.Debug.WriteLine($"Chat key gate failed to unlock: {exception}");
        }
    }

    private bool PasswordsMatch()
    {
        if (Password.Length == 0)
        {
            Message = "Enter a password.";
            return false;
        }

        if (Password != RepeatPassword)
        {
            Message = "The two passwords don't match.";
            return false;
        }

        return true;
    }

    private void SetMode(ChatKeyGateMode mode)
    {
        Mode = mode;
        Message = string.Empty;
        CodeSent = false;
        Password = string.Empty;
        RepeatPassword = string.Empty;
        Code = string.Empty;
    }

    partial void OnModeChanged(ChatKeyGateMode value)
    {
        OnPropertyChanged(nameof(IsSettingFirstPassword));
        OnPropertyChanged(nameof(IsEnteringPassword));
        OnPropertyChanged(nameof(IsResetting));
        OnPropertyChanged(nameof(IsUnlocked));
        OnPropertyChanged(nameof(CanReset));
        OnPropertyChanged(nameof(EmailAddress));
    }

    partial void OnMessageChanged(string value) => OnPropertyChanged(nameof(HasMessage));

    partial void OnCodeSentChanged(bool value) => OnPropertyChanged(nameof(AwaitingCodeRequest));
}
