using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Mobile.Chat;
using Orbit.Mobile.Crypto;
using Orbit.Mobile.Data;
using Orbit.Mobile.Sync;

namespace Orbit.Mobile.Screens.Chat;

/// <summary>
/// One group conversation. The same shape as <see cref="ConversationViewModel"/>, and everything that
/// differs comes from one fact: a group message is a separate ciphertext per member, so the sender is
/// shown per message and the fan-out happens when it goes out rather than when it is typed (see
/// info/orbit-maui-plan.md §5.5).
/// </summary>
public sealed partial class GroupConversationViewModel : ObservableObject
{
    private readonly EncryptedChatMessageReader _reader;
    private readonly EncryptedChatMessageSender _sender;
    private readonly EncryptedChatMessageEditor _editor;
    private readonly ChatRepository _chatRepository;
    private readonly ChatSynchronizer _synchronizer;
    private readonly IScreenNavigator _navigator;

    /// <summary>
    /// Slower than a one-to-one conversation's, deliberately: the group endpoint has no "since" and
    /// returns the whole history each time, so each tick costs more than it does there.
    /// </summary>
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(10);

    private LocalChatGroup? _group;
    private CancellationTokenSource? _polling;

    /// <summary>The message the compose box is currently rewriting, if it is rewriting one.</summary>
    private ReadableChatMessage? _beingEdited;
    /// <summary>
    /// True while <see cref="Status"/> is explaining something the reader just did - a message refused,
    /// an edit that could not go through. The poll runs every few seconds and would otherwise wipe the
    /// explanation before it had been read, leaving the text gone and unaccounted for again.
    /// </summary>
    private bool _statusExplainsTheLastAction;


    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _members = string.Empty;

    [ObservableProperty]
    private string _draft = string.Empty;

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private bool _isRefreshing;

    /// <inheritdoc cref="ConversationViewModel.IsEditing"/>
    [ObservableProperty]
    private bool _isEditing;

    public GroupConversationViewModel(
        EncryptedChatMessageReader reader, EncryptedChatMessageSender sender, EncryptedChatMessageEditor editor,
        ChatRepository chatRepository, ChatSynchronizer synchronizer, IScreenNavigator navigator)
    {
        _reader = reader;
        _sender = sender;
        _editor = editor;
        _chatRepository = chatRepository;
        _synchronizer = synchronizer;
        _navigator = navigator;
    }

    public ObservableCollection<ReadableChatMessage> Messages { get; } = [];

    public bool HasStatus => Status.Length > 0;

    /// <summary>
    /// A group with nobody else in it has no one to encrypt for, and the server keeps no copy for the
    /// sender - so the compose box is hidden rather than accepting messages that would vanish.
    /// </summary>
    public bool CanWrite => _group is { } group && group.Members.Count > 1;

    public void Open(LocalChatGroup group) => Show(group);

    private void Show(LocalChatGroup group)
    {
        _group = group;
        Title = group.Name;
        Members = string.Join(", ", group.Members.Select(member => member.DisplayName));
        OnPropertyChanged(nameof(CanWrite));
    }

    [RelayCommand]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        if (_group is null)
        {
            return;
        }

        await ShowStoredConversationAsync(cancellationToken);
        await RefreshMembershipAsync(cancellationToken);
        await SynchroniseAsync(cancellationToken);
    }

    /// <summary>
    /// Brings the group itself up to date, not just its messages. Done when the screen opens rather than
    /// on every poll, because it costs a lookup per member - but it has to happen somewhere: the cached
    /// keys are what opens a group message, so a member who joined since this phone last looked would
    /// have everything they write show up as unopenable.
    /// </summary>
    private async Task RefreshMembershipAsync(CancellationToken cancellationToken)
    {
        if (_group is null || !await _synchronizer.SynchroniseGroupsAsync(cancellationToken))
        {
            return;
        }

        if (await _chatRepository.FindGroupAsync(_group.Id, cancellationToken) is { } refreshed)
        {
            Show(refreshed);
        }
    }

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync(CancellationToken cancellationToken)
    {
        if (_group is null)
        {
            return;
        }

        var text = Draft.Trim();
        Draft = string.Empty;

        if (_beingEdited is { GroupMessageId: { } groupMessageId })
        {
            await RewriteAsync(groupMessageId, text, cancellationToken);
            return;
        }

        try
        {
            var result = await _sender.SendToGroupAsync(_group.Id, text, cancellationToken);
            SayWhatHappened(Describe(result));
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

    /// <inheritdoc cref="ConversationViewModel.StartEditing"/>
    [RelayCommand]
    private void StartEditing(ReadableChatMessage? message)
    {
        if (message is not { CanBeChanged: true, GroupMessageId: not null })
        {
            return;
        }

        _beingEdited = message;
        Draft = message.Text ?? string.Empty;
        IsEditing = true;
    }

    [RelayCommand]
    private void CancelEditing()
    {
        _beingEdited = null;
        Draft = string.Empty;
        IsEditing = false;
        Status = string.Empty;
    }

    [RelayCommand]
    private async Task DeleteAsync(ReadableChatMessage? message, CancellationToken cancellationToken)
    {
        if (message is not { CanBeChanged: true, MessageId: { } messageId })
        {
            return;
        }

        try
        {
            SayWhatHappened(ChatEditMessage.For(await _editor.DeleteAsync(messageId, cancellationToken)));
        }
        catch (OperationCanceledException)
        {
            return;
        }

        await ShowStoredConversationAsync(cancellationToken);
    }

    /// <summary>
    /// Rewriting a group message is the whole fan-out again, one copy per current member - leaving one
    /// behind would show different members different words.
    /// </summary>
    private async Task RewriteAsync(Guid groupMessageId, string text, CancellationToken cancellationToken)
    {
        _beingEdited = null;
        IsEditing = false;

        try
        {
            SayWhatHappened(ChatEditMessage.For(
                await _editor.EditGroupMessageAsync(_group!.Id, groupMessageId, text, cancellationToken)));
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
        await SynchroniseAsync(cancellationToken, showProgress: false);
    }

    /// <inheritdoc cref="ConversationViewModel.StartPolling"/>
    public void StartPolling()
    {
        StopPolling();
        _polling = new CancellationTokenSource();
        _ = PollAsync(_polling.Token);
    }

    public void StopPolling()
    {
        _polling?.Cancel();
        _polling?.Dispose();
        _polling = null;
    }

    private async Task PollAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(PollingInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await SynchroniseAsync(cancellationToken, showProgress: false);
            }
        }
        catch (OperationCanceledException)
        {
            // The screen went away. Nothing to report - this loop is nobody's foreground work.
        }
    }

    [RelayCommand]
    private void GoBack()
    {
        StopPolling();
        _navigator.ShowGroups();
    }

    private async Task ShowStoredConversationAsync(CancellationToken cancellationToken)
    {
        if (_group is null)
        {
            return;
        }

        try
        {
            var conversation = await _reader.ReadGroupAsync(_group.Id, cancellationToken);
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

    private async Task SynchroniseAsync(CancellationToken cancellationToken, bool showProgress = true)
    {
        if (_group is null)
        {
            return;
        }

        IsRefreshing = showProgress;
        try
        {
            var result = await _synchronizer.SynchroniseGroupConversationAsync(_group.Id, cancellationToken);
            if (!_statusExplainsTheLastAction)
            {
                Status = result.ReachedTheServer ? string.Empty : "Offline - showing what's on this phone";
            }

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

    /// <summary>See ConversationViewModel for why a refusal has to be said out loud.</summary>
    private static string Describe(ChatSendResult result)
    {
        if (!result.ReachedTheServer)
        {
            return "Offline - your message is saved and will send later";
        }

        return result.GivenUp > 0 ? ChatRefusalMessage.For(result.Refusal) : string.Empty;
    }

    partial void OnDraftChanged(string value)
    {
        // Typing is the reader moving on, so the explanation has served its purpose.
        _statusExplainsTheLastAction = false;
        SendCommand.NotifyCanExecuteChanged();
    }


    /// <summary>Says something about what the reader just did, and keeps the poll from wiping it.</summary>
    private void SayWhatHappened(string status)
    {
        Status = status;
        _statusExplainsTheLastAction = status.Length > 0;
    }

    partial void OnStatusChanged(string value) => OnPropertyChanged(nameof(HasStatus));
}
