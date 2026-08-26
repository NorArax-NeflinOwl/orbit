using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Contracts.Chat;
using Orbit.Mobile.Api;
using Orbit.Mobile.Crypto;

namespace Orbit.Maui.Features.Chat;

/// <summary>
/// Who the user can talk to. Online-only for now: contacts are not part of the local store yet (the plan
/// makes them read-only offline, which is a later step), so this says so rather than showing nothing.
/// </summary>
public sealed partial class ContactsViewModel : ObservableObject
{
	private readonly ChatClient _chatClient;
	private readonly OwnEncryptionKeyProvider _encryptionKeyProvider;
	private readonly AppNavigator _navigator;

	[ObservableProperty]
	private string _message = string.Empty;

	[ObservableProperty]
	private bool _isRefreshing;

	public ContactsViewModel(
		ChatClient chatClient, OwnEncryptionKeyProvider encryptionKeyProvider, AppNavigator navigator)
	{
		_chatClient = chatClient;
		_encryptionKeyProvider = encryptionKeyProvider;
		_navigator = navigator;
	}

	public ObservableCollection<ContactDto> Contacts { get; } = [];

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
			var contacts = await _chatClient.GetContactsAsync(cancellationToken);
			Contacts.Clear();
			foreach (var contact in contacts)
			{
				Contacts.Add(contact);
			}

			if (Contacts.Count == 0)
			{
				Message = "No conversations yet.";
			}
		}
		catch (HttpRequestException)
		{
			Message = "Couldn't reach Orbit. Your conversations aren't stored on this device yet.";
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

	[RelayCommand]
	private void OpenConversation(ContactDto? contact)
	{
		if (contact is not null)
		{
			_navigator.ShowConversation(contact);
		}
	}

	[RelayCommand]
	private void GoBack() => _navigator.ShowNotes();

	partial void OnMessageChanged(string value) => OnPropertyChanged(nameof(HasMessage));
}
