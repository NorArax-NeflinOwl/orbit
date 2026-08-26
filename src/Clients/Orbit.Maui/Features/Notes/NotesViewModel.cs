using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Mobile.Authentication;
using Orbit.Mobile.Data;
using Orbit.Mobile.Sync;

namespace Orbit.Maui.Features.Notes;

/// <summary>
/// The notes screen. Reads the local database and never the API - that is what makes it work with no
/// connection, and it is structural rather than an optimisation (info/orbit-maui-plan.md §6). The
/// synchroniser is what brings the two into step, and this screen only ever asks it to run.
/// </summary>
public sealed partial class NotesViewModel : ObservableObject
{
	private readonly LocalNoteRepository _notes;
	private readonly NoteSynchronizer _synchronizer;
	private readonly INetworkStatus _networkStatus;
	private readonly SessionStore _sessionStore;
	private readonly AuthenticationClient _authenticationClient;
	private readonly AppNavigator _navigator;

	[ObservableProperty]
	private string _greeting = string.Empty;

	[ObservableProperty]
	private string _syncStatus = string.Empty;

	[ObservableProperty]
	private string _newNoteTitle = string.Empty;

	[ObservableProperty]
	private bool _isRefreshing;

	public NotesViewModel(
		LocalNoteRepository notes, NoteSynchronizer synchronizer, INetworkStatus networkStatus,
		SessionStore sessionStore, AuthenticationClient authenticationClient, AppNavigator navigator)
	{
		_notes = notes;
		_synchronizer = synchronizer;
		_networkStatus = networkStatus;
		_sessionStore = sessionStore;
		_authenticationClient = authenticationClient;
		_navigator = navigator;
	}

	public ObservableCollection<NoteListItem> Notes { get; } = [];

	/// <summary>
	/// Shows what is already on the phone first, then synchronises. The other order would leave the
	/// screen blank for the length of a round trip, and empty for as long as there is no network at all.
	/// </summary>
	[RelayCommand]
	private async Task LoadAsync(CancellationToken cancellationToken)
	{
		if (await _sessionStore.GetAsync() is { } session)
		{
			Greeting = $"Signed in as {session.DisplayName}";
		}

		await ShowLocalNotesAsync(cancellationToken);
		await SynchroniseAsync(cancellationToken);
	}

	[RelayCommand(CanExecute = nameof(CanAddNote))]
	private async Task AddNoteAsync(CancellationToken cancellationToken)
	{
		await _notes.CreateAsync(NewNoteTitle.Trim(), NoteListItem.EmptyContent, cancellationToken);
		NewNoteTitle = string.Empty;

		await ShowLocalNotesAsync(cancellationToken);
		await SynchroniseAsync(cancellationToken);
	}

	private bool CanAddNote => NewNoteTitle.Trim().Length > 0;

	[RelayCommand]
	private void GoToTasks() => _navigator.ShowTasks();

	[RelayCommand]
	private void GoToChat() => _navigator.ShowContacts();

	[RelayCommand]
	private void GoToAccount() => _navigator.ShowAccount();

	[RelayCommand]
	private async Task SignOutAsync()
	{
		await _authenticationClient.SignOutAsync();
		_navigator.ShowSignIn();
	}

	private async Task ShowLocalNotesAsync(CancellationToken cancellationToken)
	{
		var stored = await _notes.GetAllAsync(cancellationToken);
		var pending = await _notes.GetPendingNoteLocalIdsAsync(cancellationToken);

		Notes.Clear();
		foreach (var note in stored)
		{
			Notes.Add(NoteListItem.From(note, pending.Contains(note.LocalId), _networkStatus));
		}
	}

	private async Task SynchroniseAsync(CancellationToken cancellationToken)
	{
		IsRefreshing = true;
		try
		{
			var result = await _synchronizer.SynchroniseAsync(cancellationToken);
			SyncStatus = DescribeSync(result);

			if (result.Sent + result.Received + result.RemovedLocally > 0)
			{
				await ShowLocalNotesAsync(cancellationToken);
			}
		}
		catch (HttpRequestException)
		{
			// The server was reached and refused - an expired session, most often. AppNavigator is
			// watching the session store and moves to sign-in when that is what happened; there is
			// nothing useful to say here beyond not claiming the phone is offline.
			SyncStatus = "Couldn't sync just now";
		}
		catch (OperationCanceledException)
		{
			// The screen went away mid-sync. The command is started without being awaited, so this must
			// not escape.
		}
		finally
		{
			IsRefreshing = false;
		}
	}

	/// <summary>
	/// "Offline" is only said when the phone actually believes it has no connection. A sync that failed
	/// while connectivity looks fine is a different thing - a server having a bad moment - and saying
	/// "offline" would send the user looking for a network problem that isn't there.
	/// </summary>
	private string DescribeSync(SyncResult result)
	{
		if (result.ReachedTheServer)
		{
			return result.Sent > 0 ? $"Synced - sent {result.Sent}" : "Synced";
		}

		if (!_networkStatus.IsOnline)
		{
			return "Offline - showing what's on this phone";
		}

		return "Couldn't sync just now - your changes are saved on this phone";
	}

	partial void OnNewNoteTitleChanged(string value) => AddNoteCommand.NotifyCanExecuteChanged();
}
