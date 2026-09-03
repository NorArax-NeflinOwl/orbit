using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Contracts.Chat;
using Orbit.Mobile.Api;
using Orbit.Mobile.Chat;
using Orbit.Mobile.Crypto;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Sync;

namespace Orbit.Mobile.Screens.Chat;

/// <summary>
/// One group conversation. The same shape as <see cref="ConversationViewModel"/>, and everything that
/// differs comes from one fact: a group message is a separate ciphertext per member, so the sender is
/// shown per message and the fan-out happens when it goes out rather than when it is typed (see
/// info/orbit-maui-plan.md §5.5).
/// </summary>
public sealed partial class GroupConversationViewModel : ObservableObject, IDisposable
{
    private readonly EncryptedChatMessageReader _reader;
    private readonly EncryptedChatMessageSender _sender;
    private readonly EncryptedChatMessageEditor _editor;
    private readonly ChatRepository _chatRepository;
    private readonly ChatSynchronizer _synchronizer;
    private readonly ChatClient _chatClient;
    private readonly Translations _translations;
    private readonly IScreenNavigator _navigator;

    /// <summary>
    /// Slower than a one-to-one conversation's, deliberately: the group endpoint has no "since" and
    /// returns the whole history each time, so each tick costs more than it does there.
    /// </summary>
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(10);

    /// <inheritdoc cref="ConversationViewModel"/>
    private static readonly TimeSpan ConnectedPollingInterval = TimeSpan.FromSeconds(30);

    /// <summary>What says a message arrived, so this screen can read once instead of every ten seconds.</summary>
    private readonly Live.ILiveUpdates _liveUpdates;

    private LocalChatGroup? _group;
    private CancellationTokenSource? _polling;

    /// <summary>The message the compose box is currently rewriting, if it is rewriting one.</summary>
    private ReadableChatMessage? _beingEdited;

    /// <inheritdoc cref="ConversationViewModel.StartReplying"/>
    private ReadableChatMessage? _beingAnswered;
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

    /// <inheritdoc cref="ConversationViewModel.ReplyingToPreview"/>
    [ObservableProperty]
    private string _replyingToPreview = string.Empty;

    public GroupConversationViewModel(
        EncryptedChatMessageReader reader, EncryptedChatMessageSender sender, EncryptedChatMessageEditor editor,
        ChatRepository chatRepository, ChatSynchronizer synchronizer, ChatClient chatClient,
        Translations translations, IScreenNavigator navigator, Live.ILiveUpdates liveUpdates)
    {
        _liveUpdates = liveUpdates;
        _liveUpdates.ChatChanged += OnSomethingChanged;
        _reader = reader;
        _sender = sender;
        _editor = editor;
        _chatRepository = chatRepository;
        _synchronizer = synchronizer;
        _chatClient = chatClient;
        _translations = translations;
        _navigator = navigator;
    }

    public ObservableCollection<ReadableChatMessage> Messages { get; } = [];

    public bool HasStatus => Status.Length > 0;

    /// <inheritdoc cref="ConversationViewModel.HasReplyingTo"/>
    public bool HasReplyingTo => ReplyingToPreview.Length > 0;

    partial void OnReplyingToPreviewChanged(string value) => OnPropertyChanged(nameof(HasReplyingTo));

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
        if (_group is null)
        {
            return;
        }

        try
        {
            if (!await _synchronizer.SynchroniseGroupsAsync(cancellationToken))
            {
                return;
            }
        }
        catch (HttpRequestException)
        {
            // See ContactsViewModel: this runs from OnAppearing without being awaited, so a refusal
            // escaping here would take the app down rather than showing the cached membership.
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
            var result = await _sender.SendToGroupAsync(_group.Id, Compose(text), cancellationToken);
            StopAnswering();
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

        StopAnswering();
        _beingEdited = message;
        Draft = message.Text ?? string.Empty;
        IsEditing = true;
    }

    /// <inheritdoc cref="ConversationViewModel.StartReplying"/>
    [RelayCommand]
    private void StartReplying(ReadableChatMessage? message)
    {
        if (message is not { CanBeRepliedTo: true })
        {
            return;
        }

        if (IsEditing)
        {
            CancelEditing();
        }

        _beingAnswered = message;
        ReplyingToPreview = ReplyMessagePayload.Preview(message.Text ?? string.Empty);
    }

    [RelayCommand]
    private void CancelReplying() => StopAnswering();

    private void StopAnswering()
    {
        _beingAnswered = null;
        ReplyingToPreview = string.Empty;
    }

    /// <summary>
    /// What actually goes out. A group reply names the posting rather than this device's copy of it:
    /// every member holds a different copy, and an id only this phone knows resolves for nobody else.
    /// </summary>
    private string Compose(string text)
        => _beingAnswered is { Text: { Length: > 0 } answered } answeredMessage
            && (answeredMessage.GroupMessageId ?? answeredMessage.MessageId) is { } answeredId
            ? ReplyMessage.Wrap(answeredId, answered, text)
            : text;

    /// <summary>
    /// Who this message reached and who has read it, as lines somebody can read. Composed here rather
    /// than in the page so the wording is testable without a screen - the page only has to show it.
    ///
    /// Delivered means the server holds a copy addressed to that member, which is what a receipt is. A
    /// member who joined after the message was sent has no copy and so appears in no receipt at all;
    /// they are left out rather than shown as having not read something never sent to them.
    /// </summary>
    public async Task<string> DescribeReceiptsAsync(
        ReadableChatMessage? message, CancellationToken cancellationToken = default)
    {
        if (_group is not { } group || message?.GroupMessageId is not { } groupMessageId)
        {
            return string.Empty;
        }

        IReadOnlyList<GroupMessageReceiptDto> receipts;
        try
        {
            receipts = await _chatClient.GetGroupMessageReceiptsAsync(group.Id, groupMessageId, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            return _translations["Couldn't read who has seen this."];
        }

        if (receipts.Count == 0)
        {
            return _translations["Nobody else has a copy of this yet."];
        }

        var names = group.Members.ToDictionary(member => member.UserId, member => member.DisplayName);

        return string.Join(
            Environment.NewLine,
            receipts.Select(receipt => Describe(receipt, names)));
    }

    private string Describe(GroupMessageReceiptDto receipt, IReadOnlyDictionary<Guid, string> names)
    {
        var who = names.GetValueOrDefault(receipt.RecipientUserId) ?? _translations["Someone"];

        return receipt.ReadAtUtc is { } readAt
            ? _translations.Format(
                "{0} - read {1}", who, readAt.ToLocalTime().ToString("g", _translations.DisplayCulture))
            : _translations.Format("{0} - delivered", who);
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
            SayWhatHappened(ChatEditMessage.For(await _editor.DeleteAsync(messageId, cancellationToken), _translations));
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
                await _editor.EditGroupMessageAsync(_group!.Id, groupMessageId, text, cancellationToken),
                _translations));
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
        // Slower while something is announcing changes, and back to the old pace the moment it stops -
        // see ILiveUpdates. Not switched off entirely: an announcement is best-effort, and a chat that
        // silently stopped updating because one was dropped is a far worse bug than one that takes half
        // a minute in a rare case.
        using var timer = new PeriodicTimer(_liveUpdates.IsConnected ? ConnectedPollingInterval : PollingInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                timer.Period = _liveUpdates.IsConnected ? ConnectedPollingInterval : PollingInterval;
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

    /// <summary>Who is in the group, and - for an admin - changing it.</summary>
    [RelayCommand]
    private void OpenMembers()
    {
        if (_group is not null)
        {
            StopPolling();
            _navigator.ShowGroupDetail(_group);
        }
    }

    /// <summary>
    /// The messages with the joins woven in, in the order everything happened - what Orbit.Web's thread
    /// shows and what the phone showed none of, so a newcomer watching a group's past appear had nothing
    /// telling them where it came from.
    ///
    /// Best effort: a thread that could not fetch the announcements is still the conversation, and
    /// refusing to draw it because a decoration failed would be the wrong trade.
    /// </summary>
    private async Task<IReadOnlyList<ReadableChatMessage>> WithAnnouncementsAsync(
        IReadOnlyList<ReadableChatMessage> conversation, CancellationToken cancellationToken)
    {
        if (_group is null)
        {
            return conversation;
        }

        IReadOnlyList<ChatGroupAnnouncementDto> announcements;
        try
        {
            announcements = await _chatClient.GetGroupAnnouncementsAsync(_group.Id, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            return conversation;
        }

        if (announcements.Count == 0)
        {
            return conversation;
        }

        return [.. conversation
            .Concat(announcements.Select(announcement => GroupAnnouncementLine.For(announcement, _group, _translations)))
            .OrderBy(line => line.SentAtUtc)];
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
            foreach (var line in await WithAnnouncementsAsync(conversation, cancellationToken))
            {
                Messages.Add(line);
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
                Status = result.ReachedTheServer ? string.Empty : _translations["Offline - showing what's on this phone"];
            }

            if (result.Sent + result.Received > 0)
            {
                await ShowStoredConversationAsync(cancellationToken);
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or EncryptionKeyLockedException)
        {
            Status = _translations["Couldn't sync this conversation just now"];
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
    private string Describe(ChatSendResult result)
    {
        if (!result.ReachedTheServer)
        {
            return _translations["Offline - your message is saved and will send later"];
        }

        return result.GivenUp > 0 ? ChatRefusalMessage.For(result.Refusal, _translations) : string.Empty;
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

    /// <inheritdoc cref="ConversationViewModel.OnSomethingChanged"/>
    private void OnSomethingChanged()
    {
        if (_polling is { IsCancellationRequested: false } polling)
        {
            _ = SynchroniseAsync(polling.Token, showProgress: false);
        }
    }

    public void Dispose() => _liveUpdates.ChatChanged -= OnSomethingChanged;
}
