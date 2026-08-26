using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Mobile.Chat;
using Orbit.Mobile.Data;
using Orbit.Mobile.Crypto;
using Orbit.Mobile.Sync;

namespace Orbit.Mobile.Screens.Chat;

/// <summary>
/// One conversation. Reads from the local database so history opens with no connection, and hands
/// anything typed to <see cref="EncryptedChatMessageSender"/>, which queues it and encrypts it at the
/// moment it goes out - see info/orbit-maui-plan.md §5.5.
/// </summary>
public sealed partial class ConversationViewModel : ObservableObject
{
    private readonly EncryptedChatMessageReader _reader;
    private readonly EncryptedChatMessageSender _sender;
    private readonly EncryptedChatMessageEditor _editor;
    private readonly MessageForwarder _forwarder;
    private readonly ChatRepository _chatRepository;
    private readonly ChatSynchronizer _synchronizer;
    private readonly IScreenNavigator _navigator;

    /// <summary>
    /// How often an open conversation checks for new messages. Orbit.Web polls chat once a second, which
    /// info/orbit-maui-plan.md §11 singles out as the thing not to copy literally: on a phone that costs
    /// battery and gets throttled the moment the app is backgrounded. This polls only while the screen is
    /// actually in front of someone, and at a rate a person reading a conversation would not notice.
    ///
    /// It is a stopgap either way - §4.2's silent push is what makes chat timely without a timer at all.
    /// </summary>
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(5);

    private LocalContact? _contact;
    private CancellationTokenSource? _polling;

    /// <summary>The message the compose box is currently rewriting, if it is rewriting one.</summary>
    private ReadableChatMessage? _beingEdited;

    /// <summary>
    /// How far the other party has read, as of the last sync that reached the server. Kept rather than
    /// cleared when a later sync cannot ask: "not known any more" would show as "not read", which is a
    /// worse answer than the one from a minute ago.
    /// </summary>
    private DateTimeOffset? _theyReadUpToUtc;

    /// <summary>The message waiting for somewhere to be passed on to, if one is.</summary>
    private ReadableChatMessage? _beingForwarded;
    /// <summary>
    /// True while <see cref="Status"/> is explaining something the reader just did - a message refused,
    /// an edit that could not go through. The poll runs every few seconds and would otherwise wipe the
    /// explanation before it had been read, leaving the text gone and unaccounted for again.
    /// </summary>
    private bool _statusExplainsTheLastAction;


    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _draft = string.Empty;

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private bool _isRefreshing;

    /// <summary>The compose box doubles as the editor, so the Send button has to say which it is doing.</summary>
    [ObservableProperty]
    private bool _isEditing;

    /// <summary>While picking somewhere to forward to, the list of conversations replaces the messages.</summary>
    [ObservableProperty]
    private bool _isForwarding;

    public ConversationViewModel(
        EncryptedChatMessageReader reader, EncryptedChatMessageSender sender, EncryptedChatMessageEditor editor,
        MessageForwarder forwarder, ChatRepository chatRepository, ChatSynchronizer synchronizer,
        IScreenNavigator navigator)
    {
        _reader = reader;
        _sender = sender;
        _editor = editor;
        _forwarder = forwarder;
        _chatRepository = chatRepository;
        _synchronizer = synchronizer;
        _navigator = navigator;
    }

    public ObservableCollection<ReadableChatMessage> Messages { get; } = [];

    /// <summary>Where a message can be passed on to: every conversation but this one.</summary>
    public ObservableCollection<LocalContact> ForwardTargets { get; } = [];

    public bool HasStatus => Status.Length > 0;

    /// <summary>
    /// Someone with no published key cannot be written to at all - there is nothing to encrypt for them -
    /// so the compose box is hidden rather than accepting messages that could never be sent.
    /// </summary>
    public bool CanWrite => _contact?.PublicKeyBase64 is not null;

    /// <summary>The compose box and the forward picker share the bottom of the screen, so only one shows.</summary>
    public bool CanCompose => CanWrite && !IsForwarding;

    public bool IsNotForwarding => !IsForwarding;

    public void Open(LocalContact contact)
    {
        _contact = contact;
        Title = contact.DisplayName;
        OnPropertyChanged(nameof(CanWrite));
        OnPropertyChanged(nameof(CanCompose));
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

        if (_beingEdited is { MessageId: { } messageId })
        {
            await RewriteAsync(messageId, text, cancellationToken);
            return;
        }

        try
        {
            var result = await _sender.SendAsync(_contact.UserId, text, cancellationToken);
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

    /// <summary>
    /// Puts a message back into the compose box to be rewritten. The box doubles as the editor rather
    /// than a screen of its own: there is one text field either way, and a phone has no room to spare.
    /// </summary>
    [RelayCommand]
    private void StartEditing(ReadableChatMessage? message)
    {
        if (message is not { CanBeChanged: true })
        {
            return;
        }

        _beingEdited = message;
        Draft = message.Text ?? string.Empty;
        IsEditing = true;
    }

    /// <summary>
    /// Offers somewhere to pass this message on to. Only other conversations - forwarding a message back
    /// into the one it came from is just repeating it.
    /// </summary>
    [RelayCommand]
    private async Task StartForwardingAsync(ReadableChatMessage? message, CancellationToken cancellationToken)
    {
        if (message is not { CanBeForwarded: true } || _contact is null)
        {
            return;
        }

        ForwardTargets.Clear();
        foreach (var contact in await _chatRepository.GetContactsAsync(cancellationToken))
        {
            if (contact.UserId != _contact.UserId && contact.PublicKeyBase64 is not null)
            {
                ForwardTargets.Add(contact);
            }
        }

        if (ForwardTargets.Count == 0)
        {
            SayWhatHappened("No other conversations to forward this to yet.");
            return;
        }

        _beingForwarded = message;
        IsForwarding = true;
    }

    [RelayCommand]
    private void CancelForwarding()
    {
        _beingForwarded = null;
        IsForwarding = false;
    }

    [RelayCommand]
    private async Task ForwardToAsync(LocalContact? target, CancellationToken cancellationToken)
    {
        if (target is null || _beingForwarded is not { } message || _contact is null)
        {
            return;
        }

        var forwarded = message;
        CancelForwarding();

        try
        {
            var result = await _forwarder.ForwardAsync(
                forwarded, _contact.UserId, _contact.DisplayName, target, cancellationToken);

            SayWhatHappened(result is { Sent: > 0 }
                ? $"Forwarded to {target.DisplayName}."
                : Describe(result));
        }
        catch (EncryptionKeyLockedException)
        {
            _navigator.ShowChatKeyGate();
        }
        catch (OperationCanceledException)
        {
            // The screen went away mid-forward.
        }
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
        if (message is not { CanBeChanged: true, MessageId: { } messageId } || _contact is null)
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

    private async Task RewriteAsync(Guid messageId, string text, CancellationToken cancellationToken)
    {
        _beingEdited = null;
        IsEditing = false;

        try
        {
            SayWhatHappened(ChatEditMessage.For(await _editor.EditAsync(messageId, _contact!.UserId, text, cancellationToken)));
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

    /// <summary>
    /// Starts checking for new messages, and stops when the screen goes away. Driven by the page's own
    /// lifecycle rather than a timer that outlives it, so a conversation nobody is looking at costs
    /// nothing.
    /// </summary>
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
                // No spinner: a pull-to-refresh indicator appearing by itself every few seconds reads
                // as the app struggling rather than as it working.
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
        _navigator.ShowContacts();
    }

    private async Task ShowStoredConversationAsync(CancellationToken cancellationToken)
    {
        if (_contact?.PublicKeyBase64 is not { } otherPublicKey)
        {
            Status = "This person hasn't set up chat yet.";
            return;
        }

        try
        {
            var conversation = await _reader.ReadAsync(_contact.UserId, otherPublicKey, _theyReadUpToUtc, cancellationToken);
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
        if (_contact is null)
        {
            return;
        }

        IsRefreshing = showProgress;
        try
        {
            var result = await _synchronizer.SynchroniseConversationAsync(_contact.UserId, cancellationToken);
            var readStateMoved = result.TheyReadUpToUtc != _theyReadUpToUtc;
            if (result.ReachedTheServer)
            {
                _theyReadUpToUtc = result.TheyReadUpToUtc;
            }
            if (!_statusExplainsTheLastAction)
            {
                Status = result.ReachedTheServer ? string.Empty : "Offline - showing what's on this phone";
            }

            // Redrawn when somebody has now read something too, not only when messages moved - otherwise
            // the ticks would appear whenever the next message happened to arrive.
            if (result.Sent + result.Received > 0 || (result.ReachedTheServer && readStateMoved))
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

    /// <summary>
    /// A refused message is dropped, so saying nothing would leave the text gone and unexplained. Offline
    /// is the opposite case - it is kept - and the two must not read alike.
    /// </summary>
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

    partial void OnIsForwardingChanged(bool value)
    {
        OnPropertyChanged(nameof(IsNotForwarding));
        OnPropertyChanged(nameof(CanCompose));
    }

    partial void OnStatusChanged(string value) => OnPropertyChanged(nameof(HasStatus));
}
