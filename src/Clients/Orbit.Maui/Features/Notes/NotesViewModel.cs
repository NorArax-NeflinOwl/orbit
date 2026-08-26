using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Contracts.Notes;
using Orbit.Mobile.Api;
using Orbit.Mobile.Authentication;

namespace Orbit.Maui.Features.Notes;

/// <summary>
/// The one real screen of the walking skeleton: proof that a signed-in app can read the user's own data
/// through the token handler. From phase 2 this reads from the local database instead, and the sync
/// layer keeps that current - see info/orbit-maui-plan.md §5.
/// </summary>
public sealed partial class NotesViewModel : ObservableObject
{
	private readonly NotesClient _notesClient;
	private readonly AuthenticationClient _authenticationClient;
	private readonly SessionStore _sessionStore;
	private readonly AppNavigator _navigator;

	[ObservableProperty]
	private string _greeting = string.Empty;

	[ObservableProperty]
	private string _errorMessage = string.Empty;

	[ObservableProperty]
	private bool _isRefreshing;

	public NotesViewModel(
		NotesClient notesClient, AuthenticationClient authenticationClient,
		SessionStore sessionStore, AppNavigator navigator)
	{
		_notesClient = notesClient;
		_authenticationClient = authenticationClient;
		_sessionStore = sessionStore;
		_navigator = navigator;
	}

	public ObservableCollection<NoteDto> Notes { get; } = [];

	public bool HasError => ErrorMessage.Length > 0;

	[RelayCommand]
	private async Task LoadAsync(CancellationToken cancellationToken)
	{
		if (await _sessionStore.GetAsync() is { } session)
		{
			Greeting = $"Signed in as {session.DisplayName}";
		}

		ErrorMessage = string.Empty;

		try
		{
			var notes = await _notesClient.GetAllAsync(cancellationToken);
			Notes.Clear();
			foreach (var note in notes)
			{
				Notes.Add(note);
			}
		}
		catch (HttpRequestException)
		{
			ErrorMessage = "Couldn't reach Orbit.";
		}
		catch (OperationCanceledException)
		{
			// A load abandoned because another started, or because the screen went away. Nothing to
			// report - and it must not escape, because the command is started without being awaited.
		}
		finally
		{
			// Only ever cleared here: RefreshView raises IsRefreshing itself when the user pulls, so
			// setting it from inside the command it binds to would start a second load that cancels the
			// first one.
			IsRefreshing = false;
		}
	}

	[RelayCommand]
	private async Task SignOutAsync()
	{
		await _authenticationClient.SignOutAsync();
		_navigator.ShowSignIn();
	}

	partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));
}
