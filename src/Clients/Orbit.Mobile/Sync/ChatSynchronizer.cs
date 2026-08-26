using Microsoft.Extensions.Logging;
using Orbit.Mobile.Api;
using Orbit.Mobile.Chat;
using Orbit.Mobile.Data;

namespace Orbit.Mobile.Sync;

/// <summary>What one pass over a conversation did.</summary>
public sealed record ChatSyncResult(int Sent, int Received, bool ReachedTheServer);

/// <summary>
/// Brings one conversation up to date: send what is queued, then take what arrived elsewhere. Same order
/// and same reasoning as <see cref="NoteSynchronizer"/> - pushing first means a message typed here is on
/// the server before the server's view of the conversation comes back.
///
/// Per-conversation rather than global, because that is how chat is read: the screen the user is looking
/// at is the one worth keeping current, and pulling every conversation on every tick is exactly the
/// battery cost §11 of the plan warns about.
/// </summary>
public sealed class ChatSynchronizer
{
    private readonly ChatRepository _chatRepository;
    private readonly ChatClient _chatClient;
    private readonly EncryptedChatMessageSender _sender;
    private readonly ILogger<ChatSynchronizer> _logger;

    public ChatSynchronizer(
        ChatRepository chatRepository, ChatClient chatClient, EncryptedChatMessageSender sender,
        ILogger<ChatSynchronizer> logger)
    {
        _chatRepository = chatRepository;
        _chatClient = chatClient;
        _sender = sender;
        _logger = logger;
    }

    /// <summary>
    /// Never throws for being offline, for the same reason note sync doesn't: this runs on a timer and on
    /// every screen open, and "there is no network" is an ordinary state on a phone.
    /// </summary>
    public async Task<ChatSyncResult> SynchroniseConversationAsync(
        Guid otherUserId, CancellationToken cancellationToken = default)
    {
        var push = await _sender.FlushAsync(cancellationToken);

        try
        {
            var since = await _chatRepository.LatestMessageAtAsync(otherUserId, cancellationToken);
            var messages = await _chatClient.GetConversationAsync(otherUserId, since, cancellationToken);
            await _chatRepository.StoreAsync(otherUserId, messages, cancellationToken);

            return new ChatSyncResult(push.Sent, messages.Count, ReachedTheServer: true);
        }
        catch (Exception exception) when (IsWorthRetrying(exception, cancellationToken))
        {
            _logger.LogInformation("Could not reach the server to pull chat ({Reason})", exception.Message);
            return new ChatSyncResult(push.Sent, 0, push.Sent > 0);
        }
    }

    /// <summary>
    /// Only failures another attempt could fix are swallowed. A 401 or a 403 has to surface, exactly as
    /// in NoteSynchronizer - telling a signed-out user they are offline is wrong and unactionable.
    /// </summary>
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
