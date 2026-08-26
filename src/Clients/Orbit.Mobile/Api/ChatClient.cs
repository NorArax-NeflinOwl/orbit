using System.Net;
using System.Net.Http.Json;
using Orbit.Contracts.Chat;

namespace Orbit.Mobile.Api;

/// <summary>What the server did with a message the app tried to send.</summary>
public enum SendMessageOutcome
{
    Sent,

    /// <summary>
    /// The recipient hasn't approved this conversation yet. A real state rather than an error: the app
    /// should stop offering to send until they do.
    /// </summary>
    NotApproved,

    /// <summary>The recipient no longer exists. Nothing queued against them can ever succeed.</summary>
    RecipientGone
}

public sealed record SendMessageResult(SendMessageOutcome Outcome, ChatMessageDto? Message = null);

/// <summary>What the server did with a group message the app tried to send.</summary>
public enum GroupSendOutcome
{
    Sent,

    /// <summary>
    /// The group is gone, or the sender is no longer in it. Membership decides both, and the server
    /// deliberately answers the same way to either - see IChatGroupRepository. Nothing queued for that
    /// group can ever succeed.
    /// </summary>
    NoLongerAMember,

    /// <summary>
    /// The fan-out did not match the group's membership: somebody joined or left between the app reading
    /// the member list and posting. Worth trying again, because the next attempt re-reads the list.
    /// </summary>
    MembershipChanged
}

/// <summary>
/// The chat half of the API. Everything here carries ciphertext only - the plaintext never leaves the
/// device, and Orbit.Api stores and relays what it cannot read.
/// </summary>
public sealed class ChatClient
{
    private readonly HttpClient _httpClient;

    public ChatClient(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<IReadOnlyList<ContactDto>> GetContactsAsync(CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<IReadOnlyList<ContactDto>>("api/chat/contacts", cancellationToken) ?? [];

    /// <summary>
    /// The conversation with one person. <paramref name="sinceUtc"/> asks for only what arrived after a
    /// point, which is how the app catches up without re-downloading a history it already holds.
    /// </summary>
    public async Task<IReadOnlyList<ChatMessageDto>> GetConversationAsync(
        Guid otherUserId, DateTimeOffset? sinceUtc = null, CancellationToken cancellationToken = default)
    {
        var path = $"api/chat/messages/{otherUserId}";
        if (sinceUtc is { } since)
        {
            path += $"?sinceUtc={Uri.EscapeDataString(since.UtcDateTime.ToString("O"))}";
        }

        return await _httpClient.GetFromJsonAsync<IReadOnlyList<ChatMessageDto>>(path, cancellationToken) ?? [];
    }

    /// <summary>
    /// Lets the party who did not start a conversation allow it. Until they do, the server refuses
    /// anything they try to send - see SendMessageCommandHandler - so this is what unblocks replying.
    /// </summary>
    /// <returns>False when there is no such request to approve, which a stale screen can produce.</returns>
    public async Task<bool> ApproveConversationAsync(Guid otherUserId, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsync(
            $"api/chat/conversations/{otherUserId}/approve", content: null, cancellationToken);

        if (response.StatusCode is HttpStatusCode.NotFound)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();
        return true;
    }

    /// <summary>Every group the signed-in user is in, with its current membership.</summary>
    public async Task<IReadOnlyList<ChatGroupDto>> GetGroupsAsync(CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<IReadOnlyList<ChatGroupDto>>("api/chat/groups", cancellationToken) ?? [];

    public async Task<Guid> CreateGroupAsync(CreateChatGroupRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync("api/chat/groups", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken);
    }

    /// <summary>
    /// The whole group conversation. Unlike the one-to-one endpoint this takes no "since", so it is the
    /// full history every time; the store replaces by message id, so re-reading costs bandwidth rather
    /// than duplicates.
    /// </summary>
    public async Task<IReadOnlyList<ChatMessageDto>> GetGroupConversationAsync(
        Guid groupId, CancellationToken cancellationToken = default)
        => await _httpClient.GetFromJsonAsync<IReadOnlyList<ChatMessageDto>>(
            $"api/chat/groups/{groupId}/messages", cancellationToken) ?? [];

    /// <summary>
    /// Posts one message as one ciphertext per other member. The server checks the set against the
    /// group's current membership and refuses anything that doesn't match exactly, which is what
    /// <see cref="GroupSendOutcome.MembershipChanged"/> reports.
    /// </summary>
    public async Task<GroupSendOutcome> SendGroupMessageAsync(
        Guid groupId, IReadOnlyList<GroupMessageCopyDto> copies, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            $"api/chat/groups/{groupId}/messages", new SendGroupMessageRequest(copies), cancellationToken);

        switch (response.StatusCode)
        {
            case HttpStatusCode.NotFound:
                return GroupSendOutcome.NoLongerAMember;
            case HttpStatusCode.BadRequest:
                return GroupSendOutcome.MembershipChanged;
            default:
                response.EnsureSuccessStatusCode();
                return GroupSendOutcome.Sent;
        }
    }

    /// <summary>
    /// Removes a message for everyone, not just for the caller. One copy's id is enough for a group
    /// message: the server removes every copy of the same posting - see DeleteChatMessageCommandHandler.
    /// </summary>
    /// <returns>False when it is already gone, or was never the caller's to remove.</returns>
    public async Task<bool> DeleteMessageAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.DeleteAsync($"api/chat/messages/{messageId}", cancellationToken);
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();
        return true;
    }

    /// <summary>Rewrites a one-to-one message. False when it is gone or was somebody else's to edit.</summary>
    public async Task<bool> EditMessageAsync(
        Guid messageId, EditMessageRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PutAsJsonAsync($"api/chat/messages/{messageId}", request, cancellationToken);
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();
        return true;
    }

    /// <summary>
    /// Rewrites one group message. The same fan-out as sending, because every copy is separately
    /// encrypted - leaving one behind would show different members different words.
    /// </summary>
    public async Task<GroupSendOutcome> EditGroupMessageAsync(
        Guid groupId, Guid groupMessageId, IReadOnlyList<GroupMessageCopyDto> copies,
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PutAsJsonAsync(
            $"api/chat/groups/{groupId}/messages/{groupMessageId}", new SendGroupMessageRequest(copies), cancellationToken);

        switch (response.StatusCode)
        {
            case HttpStatusCode.NotFound:
                return GroupSendOutcome.NoLongerAMember;
            case HttpStatusCode.BadRequest:
                return GroupSendOutcome.MembershipChanged;
            default:
                response.EnsureSuccessStatusCode();
                return GroupSendOutcome.Sent;
        }
    }

    public async Task<SendMessageResult> SendAsync(
        SendMessageRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync("api/chat/messages", request, cancellationToken);

        switch (response.StatusCode)
        {
            case HttpStatusCode.Forbidden:
                return new SendMessageResult(SendMessageOutcome.NotApproved);
            case HttpStatusCode.NotFound:
                return new SendMessageResult(SendMessageOutcome.RecipientGone);
            default:
                response.EnsureSuccessStatusCode();
                return new SendMessageResult(
                    SendMessageOutcome.Sent,
                    await response.Content.ReadFromJsonAsync<ChatMessageDto>(cancellationToken));
        }
    }
}
