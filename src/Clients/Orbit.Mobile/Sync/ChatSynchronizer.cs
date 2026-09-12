using Microsoft.Extensions.Logging;
using Orbit.Contracts.Chat;
using Orbit.Contracts.Users;
using Orbit.Mobile.Api;
using Orbit.Mobile.Chat;
using Orbit.Mobile.Data;

namespace Orbit.Mobile.Sync;

/// <summary>What one pass over a conversation did.</summary>
/// <param name="TheyReadUpToUtc">
/// How far the other party has read, when the server could be asked. Null for a group - reading is
/// tracked per conversation and groups have no equivalent - and null when offline, which is not the
/// same as "nothing read" and is why the screen keeps whatever it last knew.
/// </param>
public sealed record ChatSyncResult(
    int Sent, int Received, bool ReachedTheServer, DateTimeOffset? TheyReadUpToUtc = null);

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
    private readonly UsersClient _usersClient;
    private readonly EncryptedChatMessageSender _sender;
    private readonly ILogger<ChatSynchronizer> _logger;

    public ChatSynchronizer(
        ChatRepository chatRepository, ChatClient chatClient, UsersClient usersClient,
        EncryptedChatMessageSender sender, ILogger<ChatSynchronizer> logger)
    {
        _chatRepository = chatRepository;
        _chatClient = chatClient;
        _usersClient = usersClient;
        _sender = sender;
        _logger = logger;
    }

    /// <summary>
    /// Refreshes the cached contact list. Separate from a conversation's own sync because it is a
    /// different question - who can be talked to, rather than what was said - and the chat list needs it
    /// before any conversation is open.
    ///
    /// Never throws for being offline: the cached list is what the screen shows either way.
    /// </summary>
    public async Task<bool> SynchroniseContactsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _chatRepository.StoreContactsAsync(
                await _chatClient.GetContactsAsync(cancellationToken), cancellationToken);
            return true;
        }
        catch (Exception exception) when (IsWorthRetrying(exception, cancellationToken))
        {
            _logger.LogInformation("Could not refresh contacts ({Reason}); showing the cached list", exception.Message);
            return false;
        }
    }

    /// <summary>
    /// Refreshes the cached group list, along with the name and public key of everyone in those groups.
    /// Those two are not in the groups endpoint and have to be asked for per member, which is why this is
    /// a screen-open refresh rather than anything on a timer - a bulk lookup is what it would need first.
    ///
    /// The keys are cached for <b>reading</b> a group conversation offline. Sending never uses them, for
    /// the reason <see cref="LocalChatGroupMember"/> gives.
    ///
    /// Never throws for being offline: the cached list is what the screen shows either way.
    /// </summary>
    public async Task<bool> SynchroniseGroupsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var groups = await _chatClient.GetGroupsAsync(cancellationToken);
            var members = await ReadMemberDetailsAsync(groups, cancellationToken);

            await _chatRepository.StoreGroupsAsync(
                groups.Select(group => new LocalChatGroup
                {
                    Id = group.Id,
                    Name = group.Name,
                    OwnRole = group.OwnRole,
                    IsArchived = group.IsArchived,
                    UnreadCount = group.UnreadCount,
                    CreatedAtUtc = group.CreatedAtUtc,
                    Members = group.Members
                        .Select(member => new LocalChatGroupMember(
                            member.UserId,
                            member.Role,
                            members.GetValueOrDefault(member.UserId)?.DisplayName ?? "Someone",
                            members.GetValueOrDefault(member.UserId)?.PublicKeyBase64,
                            member.JoinedAtUtc))
                        .ToList()
                }).ToList(),
                cancellationToken);

            return true;
        }
        catch (Exception exception) when (IsWorthRetrying(exception, cancellationToken))
        {
            _logger.LogInformation("Could not refresh groups ({Reason}); showing the cached list", exception.Message);
            return false;
        }
    }

    /// <summary>
    /// One pass over a group conversation. The pull has no "since" to give - the group endpoint returns
    /// the whole history - so this costs more than a one-to-one pull and is worth calling less often.
    /// </summary>
    /// <inheritdoc cref="SynchroniseConversationAsync" path="/summary"/>
    public async Task<ChatSyncResult> SynchroniseGroupConversationAsync(
        Guid groupId, CancellationToken cancellationToken = default)
    {
        var push = await _sender.FlushAsync(cancellationToken);

        try
        {
            var messages = await _chatClient.GetGroupConversationAsync(groupId, cancellationToken);
            var stored = await _chatRepository.StoreGroupMessagesAsync(groupId, messages, cancellationToken);

            // Nothing is marked read here, and the count on the group's row stays as it is: a sync runs
            // on a timer, and pulling a message is not seeing it. See MarkGroupConversationReadAsync.

            return new ChatSyncResult(push.Sent, stored, ReachedTheServer: true);
        }
        catch (Exception exception) when (IsWorthRetrying(exception, cancellationToken))
        {
            _logger.LogInformation("Could not reach the server to pull a group conversation ({Reason})", exception.Message);
            return new ChatSyncResult(push.Sent, 0, push.Sent > 0);
        }
    }

    private async Task<Dictionary<Guid, UserSearchResultDto>> ReadMemberDetailsAsync(
        IReadOnlyList<ChatGroupDto> groups, CancellationToken cancellationToken)
    {
        var details = new Dictionary<Guid, UserSearchResultDto>();
        foreach (var userId in groups.SelectMany(group => group.Members).Select(member => member.UserId).Distinct())
        {
            if (await _usersClient.FindAsync(userId, cancellationToken) is { } member)
            {
                details[userId] = member;
            }
        }

        return details;
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
            var stored = await _chatRepository.StoreAsync(otherUserId, messages, cancellationToken);

            // Asked the other way round, for the reader's own messages, on a screen that is already
            // talking to the server. Their messages are not marked read here: a sync runs on a timer and
            // in the background, and pulling a message is not seeing it - see MarkConversationReadAsync.
            var theyReadUpToUtc = await _chatClient.GetReadReceiptAsync(otherUserId, cancellationToken);

            return new ChatSyncResult(push.Sent, stored, ReachedTheServer: true, theyReadUpToUtc);
        }
        catch (Exception exception) when (IsWorthRetrying(exception, cancellationToken))
        {
            _logger.LogInformation("Could not reach the server to pull chat ({Reason})", exception.Message);
            return new ChatSyncResult(push.Sent, 0, push.Sent > 0);
        }
    }

    /// <summary>
    /// Tells the server the reader has seen the other party's messages up to readUpToUtc, and takes the
    /// count on their row down with it, here and now rather than at the next refresh. Called by the
    /// conversation screen once a message has actually been on screen with the app in front - see
    /// ConversationReadState, which decides when that is.
    ///
    /// Not queued: false when the server could not be reached, and the screen offers the same mark again
    /// at its next chance - the next sync, the next scroll, coming back to the app. A mark that never
    /// goes out leaves a message unread, which the next time it is seen puts right; a queue that outlived
    /// the screen would be a read receipt sent long after anybody was looking.
    /// </summary>
    public async Task<bool> MarkConversationReadAsync(
        Guid otherUserId, DateTimeOffset readUpToUtc, CancellationToken cancellationToken = default)
    {
        try
        {
            await _chatClient.MarkConversationAsReadAsync(otherUserId, readUpToUtc, cancellationToken);
            await _chatRepository.MarkReadAsync(otherUserId, readUpToUtc, cancellationToken);
            return true;
        }
        catch (Exception exception) when (IsWorthRetrying(exception, cancellationToken))
        {
            _logger.LogInformation("Could not reach the server to mark a conversation read ({Reason})", exception.Message);
            return false;
        }
    }

    /// <summary>The same for a group, whose row carries a count of its own - see LocalChatGroup.UnreadCount.</summary>
    public async Task<bool> MarkGroupConversationReadAsync(
        Guid groupId, DateTimeOffset readUpToUtc, CancellationToken cancellationToken = default)
    {
        try
        {
            await _chatClient.MarkGroupConversationAsReadAsync(groupId, readUpToUtc, cancellationToken);
            // And the count on its row goes with it, here and now - not at the next refresh.
            await _chatRepository.MarkGroupReadAsync(groupId, cancellationToken);
            return true;
        }
        catch (Exception exception) when (IsWorthRetrying(exception, cancellationToken))
        {
            _logger.LogInformation("Could not reach the server to mark a group read ({Reason})", exception.Message);
            return false;
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
