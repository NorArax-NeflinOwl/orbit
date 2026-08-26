using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Contracts.Chat;
using Orbit.Mobile.Chat;
using Orbit.Mobile.Crypto;
using Orbit.Mobile.Sync;

namespace Orbit.Maui.Features.Chat;

/// <summary>
/// One conversation. Reads from the local database so history opens with no connection, and hands
/// anything typed to <see cref="EncryptedChatMessageSender"/>, which queues it and encrypts it at the
/// moment it goes out - see info/orbit-maui-plan.md §5.5.
/// </summary>
public sealed partial class ConversationViewModel : ObservableObject
{
	private readonly EncryptedChatMessageReader _reader;
	private readonly EncryptedChatMessageSender _sender;
	private readonly ChatSynchronizer _synchronizer;
	private readonly AppNavigator _navigator;

	private ContactDto? _contact;

	[ObservableProperty]
	private string _title = string.Empty;

	[ObservableProperty]
	private string _draft = string.Empty;

	[ObservableProperty]
	private string _status = string.Empty;

	[ObservableProperty]
	private bool _isRefreshing;

	public ConversationViewModel(
		EncryptedChatMessageReader reader, EncryptedChatMessageSender sender, ChatSynchronizer synchronizer,
		AppNavigator navigator)
	{
		_reader = reader;
		_sender = sender;
		_synchronizer = synchronizer;
		_navigator = navigator;
	}

	public ObservableCollection<ReadableChatMessage> Messages { get; } = [];

	public bool HasStatus => Status.Length > 0;

	/// <summary>
	/// Someone with no published key cannot be written to at all - there is nothing to encrypt for them -
	/// so the compose box is hidden rather than accepting messages that could never be sent.
	/// </summary>
	public bool CanWrite => _contact?.PublicKeyBase64 is not null;

	public void Open(ContactDto contact)
	{
		_contact = contact;
		Title = contact.DisplayName;
		OnPropertyChanged(nameof(CanWrite));
	}

	[RelayCommand]
	private async Task LoadAsync(CancellationToken cancellationToken)
	{
		if (_contact is null)
		{
			return;
		}

		await ShowStoredConversationAsync(cancellationToken);
		await SynchroniseAsync(cancellationToken);
	}

	[RelayCommand(CanExecute = nameof(CanSend))]
	private async Task SendAsync(CancellationToken cancellationToken)
	{
		if (_contact is null)
		{
			return;
		}

		var text = Draft.Trim();
		Draft = string.Empty;

		try
		{
			var result = await _sender.SendAsync(_contact.UserId, text, cancellationToken);
			Status = result.ReachedTheServer ? string.Empty : "Offline - your message is saved and will send later";
		}
		catch (EncryptionKeyLockedException)
		{
			_navigator.ShowChatKeyGate();
			return;
		}
		catch (OperationCanceledException)
		{
			return;
		}

		await ShowStoredConversationAsync(cancellationToken);
	}

	private bool CanSend => Draft.Trim().Length > 0 && CanWrite;

	[RelayCommand]
	private void GoBack() => _navigator.ShowContacts();

	private async Task ShowStoredConversationAsync(CancellationToken cancellationToken)
	{
		if (_contact?.PublicKeyBase64 is not { } otherPublicKey)
		{
			Status = "This person hasn't set up chat yet.";
			return;
		}

		try
		{
			var conversation = await _reader.ReadAsync(_contact.UserId, otherPublicKey, cancellationToken);
			Messages.Clear();
			foreach (var message in conversation)
			{
				Messages.Add(message);
			}
		}
		catch (EncryptionKeyLockedException)
		{
			_navigator.ShowChatKeyGate();
		}
		catch (OperationCanceledException)
		{
			// The screen went away mid-read.
		}
	}

	private async Task SynchroniseAsync(CancellationToken cancellationToken)
	{
		if (_contact is null)
		{
			return;
		}

		IsRefreshing = true;
		try
		{
			var result = await _synchronizer.SynchroniseConversationAsync(_contact.UserId, cancellationToken);
			Status = result.ReachedTheServer ? string.Empty : "Offline - showing what's on this phone";

			if (result.Sent + result.Received > 0)
			{
				await ShowStoredConversationAsync(cancellationToken);
			}
		}
		catch (Exception exception) when (exception is HttpRequestException or EncryptionKeyLockedException)
		{
			Status = "Couldn't sync this conversation just now";
		}
		catch (OperationCanceledException)
		{
			// The screen went away mid-sync.
		}
		finally
		{
			IsRefreshing = false;
		}
	}

	partial void OnDraftChanged(string value) => SendCommand.NotifyCanExecuteChanged();

	partial void OnStatusChanged(string value) => OnPropertyChanged(nameof(HasStatus));
}
