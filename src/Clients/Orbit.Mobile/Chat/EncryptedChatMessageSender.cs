using Microsoft.Extensions.Logging;
using Orbit.Contracts.Chat;
using Orbit.Mobile.Api;
using Orbit.Mobile.Crypto;
using Orbit.Mobile.Data;

namespace Orbit.Mobile.Chat;

/// <summary>What one attempt to flush the outgoing queue did.</summary>
public sealed record ChatSendResult(int Sent, int GivenUp, bool ReachedTheServer);

/// <summary>
/// Queues what the user typed and sends it when it can. The counterpart of Orbit.Web's class of the same
/// name, with one difference the plan insists on.
///
/// <b>Encryption happens at send time, never when the message is typed</b> (info/orbit-maui-plan.md
/// §5.5). For a one-to-one message that makes no difference today; for a group message it is the whole
/// design, because a group message is one ciphertext per current member and the server accepts exactly
/// one per member. A message encrypted when it was typed and sent an hour later carries a stale
/// membership list and is correctly rejected. Following the rule from the start is what stops group chat
/// needing this rewritten - and it is why the queue holds plaintext, which
/// <see cref="OutgoingChatMessage"/> explains.
/// </summary>
public sealed class EncryptedChatMessageSender
{
    /// <summary>After this many failures a message is dropped rather than blocking everything behind it.</summary>
    private const int MaximumFailedAttempts = 5;

    private readonly ChatRepository _chatRepository;
    private readonly ChatClient _chatClient;
    private readonly OwnEncryptionKeyProvider _encryptionKeyProvider;
    private readonly ILogger<EncryptedChatMessageSender> _logger;

    public EncryptedChatMessageSender(
        ChatRepository chatRepository, ChatClient chatClient, OwnEncryptionKeyProvider encryptionKeyProvider,
        ILogger<EncryptedChatMessageSender> logger)
    {
        _chatRepository = chatRepository;
        _chatClient = chatClient;
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

        IReadOnlyList<ContactDto> contacts;
        using var identity = await _encryptionKeyProvider.OpenAsync(cancellationToken);
        try
        {
            // Fetched now rather than cached, because this only ever runs online and a key that changed
            // since the message was typed must not be used.
            contacts = await _chatClient.GetContactsAsync(cancellationToken);
        }
        catch (Exception exception) when (IsWorthRetrying(exception, cancellationToken))
        {
            return new ChatSendResult(0, 0, ReachedTheServer: false);
        }

        var publicKeys = contacts
            .Where(contact => contact.PublicKeyBase64 is not null)
            .ToDictionary(contact => contact.UserId, contact => contact.PublicKeyBase64!);

        var sent = 0;
        var givenUp = 0;

        foreach (var message in queued)
        {
            if (!publicKeys.TryGetValue(message.RecipientUserId, out var recipientPublicKey))
            {
                // No published key means nothing can be encrypted for them - waiting will not change it.
                _logger.LogWarning("No public key for {RecipientUserId}; dropping a queued message", message.RecipientUserId);
                await _chatRepository.RemoveQueuedAsync(message.Id, cancellationToken);
                givenUp++;
                continue;
            }

            try
            {
                if (await SendOneAsync(identity, message, recipientPublicKey, cancellationToken))
                {
                    sent++;
                }
                else
                {
                    givenUp++;
                }

                await _chatRepository.RemoveQueuedAsync(message.Id, cancellationToken);
            }
            catch (Exception exception) when (IsWorthRetrying(exception, cancellationToken))
            {
                await _chatRepository.RecordFailedAttemptAsync(message.Id, cancellationToken);
                if (message.FailedAttempts + 1 >= MaximumFailedAttempts)
                {
                    _logger.LogWarning("Giving up on a queued message after {Attempts} attempts", message.FailedAttempts + 1);
                    await _chatRepository.RemoveQueuedAsync(message.Id, cancellationToken);
                    givenUp++;
                }

                return new ChatSendResult(sent, givenUp, ReachedTheServer: false);
            }
        }

        return new ChatSendResult(sent, givenUp, ReachedTheServer: true);
    }

    /// <summary>False when the server refused in a way that will not change - the message is dropped.</summary>
    private async Task<bool> SendOneAsync(
        ChatIdentity identity, OutgoingChatMessage message, string recipientPublicKey, CancellationToken cancellationToken)
    {
        var sealedText = identity.Encrypt(recipientPublicKey, message.Text);
        var result = await _chatClient.SendAsync(
            new SendMessageRequest(message.RecipientUserId, sealedText.CiphertextBase64, sealedText.NonceBase64),
            cancellationToken);

        if (result.Outcome is not SendMessageOutcome.Sent)
        {
            _logger.LogInformation("The server refused a queued message: {Outcome}", result.Outcome);
            return false;
        }

        // Stored straight away so the conversation shows it as sent without waiting for the next pull.
        if (result.Message is { } accepted)
        {
            await _chatRepository.StoreAsync(message.RecipientUserId, [accepted], cancellationToken);
        }

        return true;
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
}
