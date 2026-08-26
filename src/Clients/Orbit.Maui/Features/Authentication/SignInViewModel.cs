using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Mobile.Authentication;
using Orbit.Mobile.Crypto;

namespace Orbit.Maui.Features.Authentication;

public sealed partial class SignInViewModel : ObservableObject
{
	private readonly AuthenticationClient _authenticationClient;
	private readonly OwnEncryptionKeyProvider _encryptionKeyProvider;
	private readonly AppNavigator _navigator;

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
		AppNavigator navigator)
	{
		_authenticationClient = authenticationClient;
		_encryptionKeyProvider = encryptionKeyProvider;
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
				ErrorMessage = result.Message ?? "Those details weren't recognised.";
				return;
			}
		}
		catch (HttpRequestException)
		{
			// Connectivity said yes and the request still failed - a captive portal, most likely.
			ErrorMessage = "Couldn't reach Orbit. Check your connection and try again.";
			return;
		}

		// The one moment the plaintext password exists - see OwnEncryptionKeyProvider. Best-effort:
		// failing here leaves chat locked, which the user can recover from, and must not block sign-in.
		await TryUnlockChatKeyAsync(Password, cancellationToken);

		Password = string.Empty;
		_navigator.ShowNotes();
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
