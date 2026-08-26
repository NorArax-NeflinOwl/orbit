using Microsoft.Extensions.Logging;
using Orbit.Contracts.Chat;
using Orbit.Mobile.Api;
using Orbit.Mobile.Crypto;
using Orbit.Mobile.Data;

namespace Orbit.Mobile.Chat;

/// <summary>
/// Why a message was dropped rather than sent. Carried out to the screen because the alternative -
/// what this used to do - was to log it and let the text disappear with nothing said.
/// </summary>
public enum ChatSendRefusal
{
    None,

    /// <summary>The other party started this conversation and has not been accepted yet, so the server
    /// refuses anything sent back until they are - see ChatClient.ApproveConversationAsync.</summary>
    WaitingToBeAccepted,

    /// <summary>Somebody it had to be sealed for has no chat key published, so there is nothing to
    /// encrypt with. In a group that stops the whole message: a partial fan-out is refused by design.</summary>
    SomebodyHasNoChatKey,

    /// <summary>The recipient's account, or the group, is gone - or this account was removed from it.</summary>
    NoLongerThere
}

/// <summary>What one attempt to flush the outgoing queue did.</summary>
/// <param name="Refusal">
/// Why the most recent drop happened, when one did. <see cref="ChatSendRefusal.None"/> whenever
/// <paramref name="GivenUp"/> is zero.
/// </param>
public sealed record ChatSendResult(
    int Sent, int GivenUp, bool ReachedTheServer, ChatSendRefusal Refusal = ChatSendRefusal.None);

/// <summary>
/// Queues what the user typed and sends it when it can. The counterpart of Orbit.Web's class of the same
/// name, with one difference the plan insists on.
///
/// <b>Encryption happens at send time, never when the message is typed</b> (info/orbit-maui-plan.md
/// §5.5). For a one-to-one message that makes no difference; for a group message it is the whole design,
/// because a group message is one ciphertext per current member and the server accepts exactly one per
/// member. A message encrypted when it was typed and sent an hour later carries a stale membership list
/// and is correctly rejected. Following the rule from the start is what let group chat arrive as a
/// fan-out inside a working outbox rather than as a rewrite of one - and it is why the queue holds
/// plaintext, which <see cref="OutgoingChatMessage"/> explains.
/// </summary>
public sealed class EncryptedChatMessageSender
{
    /// <summary>After this many failures a message is dropped rather than blocking everything behind it.</summary>
    private const int MaximumFailedAttempts = 5;

    private readonly ChatRepository _chatRepository;
    private readonly ChatClient _chatClient;
    private readonly ChatDirectoryReader _directoryReader;
    private readonly OwnEncryptionKeyProvider _encryptionKeyProvider;
    private readonly ILogger<EncryptedChatMessageSender> _logger;

    public EncryptedChatMessageSender(
        ChatRepository chatRepository, ChatClient chatClient, ChatDirectoryReader directoryReader,
        OwnEncryptionKeyProvider encryptionKeyProvider, ILogger<EncryptedChatMessageSender> logger)
    {
        _chatRepository = chatRepository;
        _chatClient = chatClient;
        _directoryReader = directoryReader;
        _encryptionKeyProvider = encryptionKeyProvider;
        _logger = logger;
    }

    /// <summary>
    /// Accepts the message and tries to send it. Queuing first means a message typed with no connection
    /// is kept rather than refused, and the screen can show it as waiting.
    /// </summary>
    public async Task<ChatSendResult> SendAsync(
        Guid recipientUserId, string text, CancellationToken cancellationToken = default)
    {
        await _chatRepository.QueueAsync(recipientUserId, text, cancellationToken);
        return await FlushAsync(cancellationToken);
    }

    /// <inheritdoc cref="SendAsync"/>
    public async Task<ChatSendResult> SendToGroupAsync(
        Guid groupId, string text, CancellationToken cancellationToken = default)
    {
        await _chatRepository.QueueForGroupAsync(groupId, text, cancellationToken);
        return await FlushAsync(cancellationToken);
    }

    /// <summary>
    /// Sends everything queued, in order, stopping at the first failure that trying again could fix -
    /// reordering messages would be worse than delaying them.
    /// </summary>
    public async Task<ChatSendResult> FlushAsync(CancellationToken cancellationToken = default)
    {
        var queued = await _chatRepository.GetQueuedAsync(cancellationToken);
        if (queued.Count == 0)
        {
            return new ChatSendResult(0, 0, ReachedTheServer: true);
        }

        using var identity = await _encryptionKeyProvider.OpenAsync(cancellationToken);

        ChatDirectory directory;
        try
        {
            directory = await ReadDirectoryAsync(queued, cancellationToken);
        }
        catch (Exception exception) when (IsWorthRetrying(exception, cancellationToken))
        {
            return new ChatSendResult(0, 0, ReachedTheServer: false);
        }

        var progress = new SendProgress();
        foreach (var message in queued)
        {
            await SendOneAsync(identity, directory, message, progress, cancellationToken);
            if (!progress.KeepGoing)
            {
                break;
            }
        }

        return progress.ToResult();
    }

    private Task<ChatDirectory> ReadDirectoryAsync(
        IReadOnlyList<OutgoingChatMessage> queued, CancellationToken cancellationToken)
        => _directoryReader.ReadAsync(
            queued.Where(message => message.RecipientUserId is not null).Select(message => message.RecipientUserId!.Value).ToHashSet(),
            queued.Where(message => message.GroupId is not null).Select(message => message.GroupId!.Value).ToHashSet(),
            cancellationToken);

    private async Task SendOneAsync(
        ChatIdentity identity, ChatDirectory directory, OutgoingChatMessage message, SendProgress progress,
        CancellationToken cancellationToken)
    {
        try
        {
            var outcome = message.GroupId is { } groupId
                ? await SendToGroupAsync(identity, directory, groupId, message, cancellationToken)
                : await SendToPersonAsync(identity, directory, message, cancellationToken);

            if (outcome.IsWorthAnotherAttempt)
            {
                // The server was reached and said no in a way the next flush may not hit - it re-reads
                // the membership - so this is a delay rather than an outage.
                await RecordFailedAttemptAsync(message, progress, cancellationToken);
                progress.KeepGoing = false;
                return;
            }

            if (outcome.IsSent)
            {
                progress.Sent++;
            }
            else
            {
                progress.GiveUp(outcome.Refusal);
            }

            await _chatRepository.RemoveQueuedAsync(message.Id, cancellationToken);
        }
        catch (Exception exception) when (IsWorthRetrying(exception, cancellationToken))
        {
            await RecordFailedAttemptAsync(message, progress, cancellationToken);
            progress.ReachedTheServer = false;
            progress.KeepGoing = false;
        }
    }

    private async Task<QueuedSendOutcome> SendToPersonAsync(
        ChatIdentity identity, ChatDirectory directory, OutgoingChatMessage message, CancellationToken cancellationToken)
    {
        var recipientUserId = message.RecipientUserId!.Value;
        if (directory.FindPublicKey(recipientUserId) is not { } recipientPublicKey)
        {
            // No published key means nothing can be encrypted for them - waiting will not change it.
            _logger.LogWarning("No public key for {RecipientUserId}; dropping a queued message", recipientUserId);
            return QueuedSendOutcome.GiveUp(ChatSendRefusal.SomebodyHasNoChatKey);
        }

        var sealedText = identity.Encrypt(recipientPublicKey, message.Text);
        var result = await _chatClient.SendAsync(
            new SendMessageRequest(recipientUserId, sealedText.CiphertextBase64, sealedText.NonceBase64),
            cancellationToken);

        if (result.Outcome is not SendMessageOutcome.Sent)
        {
            _logger.LogInformation("The server refused a queued message: {Outcome}", result.Outcome);
            return QueuedSendOutcome.GiveUp(result.Outcome is SendMessageOutcome.NotApproved
                ? ChatSendRefusal.WaitingToBeAccepted
                : ChatSendRefusal.NoLongerThere);
        }

        // Stored straight away so the conversation shows it as sent without waiting for the next pull.
        if (result.Message is { } accepted)
        {
            await _chatRepository.StoreAsync(recipientUserId, [accepted], cancellationToken);
        }

        return QueuedSendOutcome.WasSent;
    }

    /// <summary>
    /// The fan-out: one ciphertext per other member, sealed here and now. The server can do none of this
    /// - it has no key to read the text with - and checks the set of copies against the group's current
    /// membership, refusing anything that isn't exactly one each.
    /// </summary>
    private async Task<QueuedSendOutcome> SendToGroupAsync(
        ChatIdentity identity, ChatDirectory directory, Guid groupId, OutgoingChatMessage message,
        CancellationToken cancellationToken)
    {
        if (directory.FindOtherMembers(groupId) is not { } otherMembers)
        {
            _logger.LogWarning("Group {GroupId} is gone, or this account is no longer in it; dropping a queued message", groupId);
            return QueuedSendOutcome.GiveUp(ChatSendRefusal.NoLongerThere);
        }

        if (otherMembers.Count == 0)
        {
            // Nobody left to encrypt for. The server stores a copy per recipient and none for the sender,
            // so posting this would be accepted and keep nothing - there is no message to be had.
            _logger.LogWarning("Group {GroupId} has no other members; dropping a queued message", groupId);
            return QueuedSendOutcome.GiveUp(ChatSendRefusal.NoLongerThere);
        }

        var copies = new List<GroupMessageCopyDto>(otherMembers.Count);
        foreach (var memberUserId in otherMembers)
        {
            if (directory.FindPublicKey(memberUserId) is not { } memberPublicKey)
            {
                // A partial fan-out is refused by design, so one member without a published key stops the
                // whole message. Dropped rather than held, because somebody who has never signed in may
                // never publish one and everything behind this would wait forever.
                _logger.LogWarning(
                    "No public key for {MemberUserId} in group {GroupId}; dropping a queued message", memberUserId, groupId);
                return QueuedSendOutcome.GiveUp(ChatSendRefusal.SomebodyHasNoChatKey);
            }

            var sealedText = identity.Encrypt(memberPublicKey, message.Text);
            copies.Add(new GroupMessageCopyDto(memberUserId, sealedText.CiphertextBase64, sealedText.NonceBase64));
        }

        var outcome = await _chatClient.SendGroupMessageAsync(groupId, copies, cancellationToken);
        if (outcome is GroupSendOutcome.MembershipChanged)
        {
            _logger.LogInformation("Group {GroupId} changed while a message was going out; it will be sent again", groupId);
            return QueuedSendOutcome.TryAgain;
        }

        return outcome is GroupSendOutcome.Sent
            ? QueuedSendOutcome.WasSent
            : QueuedSendOutcome.GiveUp(ChatSendRefusal.NoLongerThere);
    }

    private async Task RecordFailedAttemptAsync(
        OutgoingChatMessage message, SendProgress progress, CancellationToken cancellationToken)
    {
        await _chatRepository.RecordFailedAttemptAsync(message.Id, cancellationToken);
        if (message.FailedAttempts + 1 < MaximumFailedAttempts)
        {
            return;
        }

        _logger.LogWarning("Giving up on a queued message after {Attempts} attempts", message.FailedAttempts + 1);
        await _chatRepository.RemoveQueuedAsync(message.Id, cancellationToken);
        progress.GiveUp(ChatSendRefusal.NoLongerThere);
    }

    private static bool IsWorthRetrying(Exception exception, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        return exception switch
        {
            HttpRequestException { StatusCode: null } => true,
            HttpRequestException { StatusCode: { } status } =>
                (int)status >= 500 || status is System.Net.HttpStatusCode.RequestTimeout
                    or System.Net.HttpStatusCode.TooManyRequests,
            TaskCanceledException => true,
            _ => false
        };
    }

    /// <summary>
    /// What became of one queued message. <see cref="Refusal"/> is what the screen needs and the log
    /// alone could not give it.
    /// </summary>
    private sealed record QueuedSendOutcome(bool IsWorthAnotherAttempt, ChatSendRefusal Refusal)
    {
        public static readonly QueuedSendOutcome WasSent = new(IsWorthAnotherAttempt: false, ChatSendRefusal.None);

        /// <summary>Refused for something the next flush may find resolved - the membership it read moved on.</summary>
        public static readonly QueuedSendOutcome TryAgain = new(IsWorthAnotherAttempt: true, ChatSendRefusal.None);

        /// <summary>Refused in a way that will not change, so the message is dropped rather than retried.</summary>
        public static QueuedSendOutcome GiveUp(ChatSendRefusal refusal) => new(IsWorthAnotherAttempt: false, refusal);

        public bool IsSent => !IsWorthAnotherAttempt && Refusal is ChatSendRefusal.None;
    }

    /// <summary>What the flush has managed so far, and whether it should carry on.</summary>
    private sealed class SendProgress
    {
        public int Sent { get; set; }

        public int GivenUp { get; private set; }

        public ChatSendRefusal Refusal { get; private set; } = ChatSendRefusal.None;

        public bool ReachedTheServer { get; set; } = true;

        public bool KeepGoing { get; set; } = true;

        public void GiveUp(ChatSendRefusal refusal)
        {
            GivenUp++;
            Refusal = refusal;
        }

        public ChatSendResult ToResult() => new(Sent, GivenUp, ReachedTheServer, Refusal);
    }
}
