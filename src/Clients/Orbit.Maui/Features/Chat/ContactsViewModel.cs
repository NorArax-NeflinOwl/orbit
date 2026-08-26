using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Mobile.Api;
using Orbit.Mobile.Crypto;
using Orbit.Mobile.Data;
using Orbit.Mobile.Sync;

namespace Orbit.Maui.Features.Chat;

/// <summary>
/// Who the user can talk to. Read from the local cache and then refreshed, so the list opens with no
/// connection - without that, a conversation whose history is cached still could not be reached, which
/// made offline chat readable in principle and not in practice.
/// </summary>
public sealed partial class ContactsViewModel : ObservableObject
{
	private readonly ChatRepository _chatRepository;
	private readonly ChatClient _chatClient;
	private readonly ChatSynchronizer _synchronizer;
	private readonly OwnEncryptionKeyProvider _encryptionKeyProvider;
	private readonly AppNavigator _navigator;

	[ObservableProperty]
	private string _message = string.Empty;

	[ObservableProperty]
	private bool _isRefreshing;

	public ContactsViewModel(
		ChatRepository chatRepository, ChatClient chatClient, ChatSynchronizer synchronizer,
		OwnEncryptionKeyProvider encryptionKeyProvider, AppNavigator navigator)
	{
		_chatRepository = chatRepository;
		_chatClient = chatClient;
		_synchronizer = synchronizer;
		_encryptionKeyProvider = encryptionKeyProvider;
		_navigator = navigator;
	}

	public ObservableCollection<LocalContact> Contacts { get; } = [];

	public bool HasMessage => Message.Length > 0;

	[RelayCommand]
	private async Task LoadAsync(CancellationToken cancellationToken)
	{
		Message = string.Empty;

		// Chat is unreadable without the key, so ask for it before showing a list that goes nowhere.
		if (!await _encryptionKeyProvider.HasKeyAsync(cancellationToken))
		{
			_navigator.ShowChatKeyGate();
			return;
		}

		IsRefreshing = true;
		try
		{
			// What is already known first, so the list is never blank while a request is in flight.
			await ShowCachedContactsAsync(cancellationToken);

			if (await _synchronizer.SynchroniseContactsAsync(cancellationToken))
			{
				await ShowCachedContactsAsync(cancellationToken);
			}
			else
			{
				Message = Contacts.Count == 0
					? "Offline, and this device hasn't seen your conversations yet."
					: "Offline - showing what's on this phone";
			}
		}
		catch (OperationCanceledException)
		{
			// The screen went away mid-load; the command is started without being awaited.
		}
		finally
		{
			IsRefreshing = false;
		}
	}

	private async Task ShowCachedContactsAsync(CancellationToken cancellationToken)
	{
		var contacts = await _chatRepository.GetContactsAsync(cancellationToken);
		Contacts.Clear();
		foreach (var contact in contacts)
		{
			Contacts.Add(contact);
		}

		Message = Contacts.Count == 0 ? "No conversations yet." : string.Empty;
	}

	[RelayCommand]
	private void OpenConversation(LocalContact? contact)
	{
		if (contact is not null)
		{
			_navigator.ShowConversation(contact);
		}
	}

	/// <summary>
	/// Allows a conversation somebody else started. Until this happens the server refuses everything sent
	/// back, so without it a chat request that arrived from another device could be read and never
	/// answered. Needs a connection: it is the server's record of consent, not the phone's.
	/// </summary>
	[RelayCommand]
	private async Task AcceptAsync(LocalContact? contact, CancellationToken cancellationToken)
	{
		if (contact is null)
		{
			return;
		}

		try
		{
			await _chatClient.ApproveConversationAsync(contact.UserId, cancellationToken);
		}
		catch (HttpRequestException)
		{
			Message = "Accepting a chat request needs a connection.";
			return;
		}
		catch (OperationCanceledException)
		{
			return;
		}

		await LoadAsync(cancellationToken);
	}

	[RelayCommand]
	private void OpenGroups() => _navigator.ShowGroups();

	[RelayCommand]
	private void GoBack() => _navigator.ShowNotes();

	partial void OnMessageChanged(string value) => OnPropertyChanged(nameof(HasMessage));
}
