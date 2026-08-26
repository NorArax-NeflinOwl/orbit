using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Mobile.Authentication;
using Orbit.Mobile.Crypto;
using Orbit.Mobile.Sync;

namespace Orbit.Maui.Features.Account;

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
	private readonly AppNavigator _navigator;

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

	public AccountViewModel(
		AccountClient accountClient, OwnEncryptionKeyProvider encryptionKeyProvider, INetworkStatus networkStatus,
		SessionStore sessionStore, AppNavigator navigator)
	{
		_accountClient = accountClient;
		_encryptionKeyProvider = encryptionKeyProvider;
		_networkStatus = networkStatus;
		_sessionStore = sessionStore;
		_navigator = navigator;
	}

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
				Message = "Password changed, but your chat key backup couldn't be updated. " +
					"Open \"Chat key\" to fix it, or older messages may not open on a new device.";
			}
		}
		catch (Exception exception) when (exception is not OperationCanceledException)
		{
			MessageIsFailure = true;
			Message = "Password changed, but your chat key backup couldn't be updated. " +
				"Sign in again while online to fix it.";
			System.Diagnostics.Debug.WriteLine($"Could not re-wrap the chat key backup: {exception}");
		}
	}

	[RelayCommand]
	private void GoToChatKey() => _navigator.ShowChatKeyGate();

	[RelayCommand]
	private void GoBack() => _navigator.ShowNotes();

	private async Task RunAsync(Func<Task<AccountOperationResult>> operation, string successMessage)
	{
		try
		{
			var result = await operation();
			MessageIsFailure = !result.Succeeded;
			Message = result.Succeeded ? successMessage : result.Message ?? "That didn't work.";
		}
		catch (HttpRequestException)
		{
			MessageIsFailure = true;
			Message = "Couldn't reach Orbit. Check your connection and try again.";
		}
		catch (OperationCanceledException)
		{
			// The screen went away mid-request; there is nobody left to tell.
		}
	}

	partial void OnMessageChanged(string value) => OnPropertyChanged(nameof(HasMessage));
}
