using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Mobile.Authentication;

namespace Orbit.Maui.Features.Authentication;

public sealed partial class SignInViewModel : ObservableObject
{
	private readonly AuthenticationClient _authenticationClient;
	private readonly AppNavigator _navigator;

	[ObservableProperty]
	[NotifyCanExecuteChangedFor(nameof(SignInCommand))]
	private string _emailOrUserName = string.Empty;

	[ObservableProperty]
	[NotifyCanExecuteChangedFor(nameof(SignInCommand))]
	private string _password = string.Empty;

	[ObservableProperty]
	private string _errorMessage = string.Empty;

	public SignInViewModel(AuthenticationClient authenticationClient, AppNavigator navigator)
	{
		_authenticationClient = authenticationClient;
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
			if (!await _authenticationClient.SignInAsync(EmailOrUserName, Password, cancellationToken))
			{
				ErrorMessage = "Those details weren't recognised.";
				return;
			}
		}
		catch (HttpRequestException)
		{
			// Told apart from a refusal on purpose: sending someone to reset a password that was fine,
			// because their train went into a tunnel, is a bad way to lose an account.
			ErrorMessage = "Couldn't reach Orbit. Check your connection and try again.";
			return;
		}

		Password = string.Empty;
		_navigator.ShowNotes();
	}

	partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));
}
